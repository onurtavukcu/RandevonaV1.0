using Data.MongoDbContext;
using Data.Repositories;
using Domain.Entities.Identity.UserEntity;
using Domain.Models.Identity.User.Password;
using Domain.Models.Identity.User.Settings;
using IdentityService.PasswordService;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

public static class PasswordChecks
{
    public static async Task<int> RunAsync()
    {
        var count = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            count++;
            Console.WriteLine("PASS: " + name);
        }
        async Task Reject(Func<Task> action, string name)
        {
            var rejected = false;
            try { await action(); } catch { rejected = true; }
            Check(rejected, name);
        }
        const string db = "PasswordChecks";
        var mongo = new MemoryMongo();
        var control = new ControlMongoDbContext(mongo.Client,
            new MongoSettings { ConnectionString = "mongodb://127.0.0.1:1", DatabaseName = db });
        var repository = new Repository<Users>(control);

        var settings = new JwtSettings { Key = new string('k', 48), Issuer = "checks", Audience = "checks" };
        var service = new PasswordService(settings, repository, NullLogger<PasswordService>.Instance);
        var hash = service.HashPassword("secret");
        Check(service.VerifyPassword("secret", hash), "Password shorter than 12 characters verifies");
        Check(hash != service.HashPassword("secret"), "BCrypt uses a fresh salt");
        Check(!service.VerifyPassword("other", hash), "Incorrect password rejected");
        Check(!service.VerifyPassword("secret", "broken") && !service.VerifyPassword("secret", "$2b$11$bad"), "Malformed hashes rejected");
        Check(!service.VerifyPassword("", hash), "Empty password rejected");
        await Reject(() => Task.FromResult(service.HashPassword(new string('ş', 37))), "BCrypt UTF-8 byte limit enforced");

        var user = new Users { Email = "person@example.invalid", NormalizedEmail = "PERSON@EXAMPLE.INVALID", PasswordHash = hash };
        mongo.Seed(db, user);
        Task<Users?> Read() => repository.GetByIdAsync(user.Id);
        Task<long> SetState(ForgotPassword? state) => repository.UpdateManyAsync(x => x.Id == user.Id,
            Builders<Users>.Update.Set(x => x.ForgotPassword, state));
        async Task<(string Token, string Code)> Start()
        {
            var started = await service.ForgotPassword(new Users { Id = user.Id, Email = "untrusted@example.invalid" });
            Check(started.IsSuccess, "Password reset starts with persisted account");
            return (started.Value!, (await Read())!.ForgotPassword!.VerificationCode);
        }
        VerificationCodeRequest Confirmation(string token, string code) => new()
            { Email = " Person@Example.Invalid ", ResetToken = token, VerificationCode = code };
        ResetPasswordRequest Reset(string token) => new()
            { Email = "person@example.invalid", ResetToken = token, NewPassword = "new-secret", ConfirmPassword = "new-secret" };

        var first = await Start();
        Check((await Read())!.Email == user.Email, "Reset preserves the persisted email without sending mail");
        Check(!(await service.ResetPassword(Reset(first.Token))).IsSuccess, "Reset cannot bypass code confirmation");
        var wrong = first.Code == "111111" ? "222222" : "111111";
        Check(!(await service.ConfirmVerificationCode(Confirmation(first.Token, wrong))).IsSuccess, "Wrong verification code rejected");
        Check((await Read())!.ForgotPassword!.VerificationAttempts == 1, "Failed code attempt persisted");
        Check(!(await service.ConfirmVerificationCode(Confirmation("wrong-token", first.Code))).IsSuccess, "Wrong reset token rejected");
        Check((await service.ConfirmVerificationCode(Confirmation(first.Token, first.Code))).IsSuccess, "Valid code confirms request");
        var mismatch = Reset(first.Token); mismatch.ConfirmPassword = "different";
        Check(!(await service.ResetPassword(mismatch)).IsSuccess, "Password confirmation mismatch rejected");
        Check((await service.ResetPassword(Reset(first.Token))).IsSuccess, "Confirmed reset changes password");
        var changed = (await Read())!;
        Check(service.VerifyPassword("new-secret", changed.PasswordHash) && changed.ForgotPassword!.IsUsed &&
            changed.ForgotPassword.ResetToken == "", "Password and token consumption persisted together");
        Check(!(await service.ResetPassword(Reset(first.Token))).IsSuccess, "Used reset token cannot be replayed");

        var expired = await Start();
        var state = (await Read())!.ForgotPassword!; state.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await SetState(state);
        Check(!(await service.ConfirmVerificationCode(Confirmation(expired.Token, expired.Code))).IsSuccess, "Expired verification code rejected");
        state.IsVerified = true; await SetState(state);
        Check(!(await service.ResetPassword(Reset(expired.Token))).IsSuccess, "Expired confirmed token rejected");

        var limited = await Start();
        wrong = limited.Code == "111111" ? "222222" : "111111";
        for (var i = 0; i < 5; i++) await service.ConfirmVerificationCode(Confirmation(limited.Token, wrong));
        Check(!(await service.ConfirmVerificationCode(Confirmation(limited.Token, limited.Code))).IsSuccess,
            "Five failed code attempts prevent further confirmation");
        var old = await Start();
        var current = await Start();
        Check(!(await service.ConfirmVerificationCode(Confirmation(old.Token, old.Code))).IsSuccess, "New reset invalidates old token");
        Check((await service.ConfirmVerificationCode(Confirmation(current.Token, current.Code))).IsSuccess, "New reset starts a fresh verification budget");

        // Both requests read a valid snapshot before either writes. Only one update may consume it.
        var readers = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Users?> SynchronizedRead(Task<Users?> read)
        {
            var snapshot = await read;
            if (Interlocked.Increment(ref readers) == 2) gate.SetResult();
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return snapshot;
        }
        var concurrentRepository = InterfaceProxy.Create<IRepository<Users>>((method, args) =>
        {
            var result = method.Invoke(repository, args);
            return method.Name == "FindOneAsync" ? SynchronizedRead((Task<Users?>)result!) : result;
        });
        var concurrentService = new PasswordService(settings, concurrentRepository, NullLogger<PasswordService>.Instance);
        var results = await Task.WhenAll(concurrentService.ResetPassword(Reset(current.Token)), concurrentService.ResetPassword(Reset(current.Token)));
        Check(results.Count(x => x.IsSuccess) == 1, "Concurrent reset requests have exactly one winner");

        await repository.UpdateManyAsync(x => x.Id == user.Id, Builders<Users>.Update.Set(x => x.IsActive, false));
        Check(!(await service.ForgotPassword(user)).IsSuccess, "Inactive user cannot start reset");
        return count;
    }
}
