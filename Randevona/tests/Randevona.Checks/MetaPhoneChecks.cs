using Domain.Models.Meta;
using IntegrationServices.Whatsapp;
using System.Net;
using System.Text;

public static class MetaPhoneChecks
{
    public static async Task<int> RunAsync()
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); checks++; Console.WriteLine("PASS: "+name); }
        using var handler=new Handler();
        using var http=new HttpClient(handler) {MaxResponseContentBufferSize=262144};
        var settings=new WhatsAppApiSettings();
        var client=new MetaPhoneClient(http,settings);
        Task<Domain.Models.Shared.Result.Result<MetaPhoneAccess>> Read(CancellationToken ct=default) => client.CheckAccessAsync("1234567890","2345678901","secret-test-token",ct);
        Check((await Read()).Error?.Code=="Meta.NotConfigured" && handler.Calls==0, "Blank Graph API version disables external calls");
        settings.GraphApiVersion="v24.0/../../other";
        Check((await Read()).Error?.Code=="Meta.NotConfigured" && handler.Calls==0, "Graph API version cannot change endpoint path");
        settings.GraphApiVersion="v24.0"; // Test fixture version, not an application default.
        handler.Response=(_,_)=> Task.FromResult(Json("""{"data":[{"id":"2345678901","display_phone_number":"+90 555 123 45 67","verified_name":"Test"}]}"""));
        var found=await Read();
        Check(found.IsSuccess && found.Value!.PhoneNumberId=="2345678901" && handler.LastUri!.Host=="graph.facebook.com" &&
            handler.LastUri.AbsolutePath=="/v24.0/1234567890/phone_numbers" && !handler.LastUri.ToString().Contains("secret-test-token") &&
            handler.LastAuthorization=="Bearer secret-test-token", "Meta client uses fixed host and Authorization header without token in URL");
        var prior=handler.Calls;
        handler.Response=(request,_) => Task.FromResult(request.RequestUri!.Query.Contains("after=")
            ? Json("""{"data":[{"id":"2345678901","display_phone_number":"+905551234567"}]}""")
            : Json("""{"data":[],"paging":{"next":"http://untrusted.invalid/steal","cursors":{"after":"cursor&access_token=injected"}}}"""));
        Check((await Read()).IsSuccess && handler.Calls==prior+2 && handler.LastUri!.Host=="graph.facebook.com" &&
            handler.LastUri.Query.Contains("after=cursor%26access_token%3Dinjected"), "Paging uses encoded cursor on fixed host, never remote next URL");
        handler.Response=(_,_)=>Task.FromResult(Json("""{"data":[]}"""));
        Check((await Read()).Error?.Code=="Meta.NumberNotFound", "Absent number cannot be verified");
        handler.Response=(_,_)=>Task.FromResult(Json("""{"error":{"message":"secret-test-token"}}""",HttpStatusCode.Unauthorized));
        Check((await Read()).Error?.Code=="Meta.AccessDenied", "Meta authentication failure returns safe error");
        handler.Response=(_,_)=>Task.FromResult(Json("secret-test-token",HttpStatusCode.TooManyRequests));
        Check((await Read()).Error?.Code=="Meta.RateLimited", "Meta rate limit is returned without retries or raw body");
        handler.Response=(_,_)=>Task.FromResult(Json("secret-test-token",HttpStatusCode.BadRequest));
        var failed=await Read();
        Check(!failed.IsSuccess && !failed.Error!.Message.Contains("secret-test-token"), "Other Meta failures never expose provider response");
        handler.Response=(_,_)=>Task.FromResult(Json("not-json-secret-test-token"));
        Check((await Read()).Error?.Code=="Meta.Unavailable", "Malformed success response cannot mark connection verified");
        handler.Response=(_,_)=>Task.FromResult(Json("""{"data":[],"paging":{"next":"https://example.invalid","cursors":{"after":"same"}}}"""));
        prior=handler.Calls;
        Check(!(await Read()).IsSuccess && handler.Calls==prior+2, "Repeated paging cursor cannot cause unbounded loop");
        handler.Response=(_,_)=>Task.FromResult(Json(new string('x',300000)));
        Check(!(await Read()).IsSuccess, "Oversized Meta response is rejected");
        handler.Response=(_,_)=>throw new HttpRequestException("secret-test-token");
        Check(!(await Read()).Error!.Message.Contains("secret-test-token"), "Network exception messages are redacted");
        using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
        bool cancelled=false; try { await Read(cancellation.Token); } catch(OperationCanceledException) {cancelled=true;}
        Check(cancelled,"Caller cancellation propagates without recording verification");
        return checks;
    }
    private static HttpResponseMessage Json(string body,HttpStatusCode status=HttpStatusCode.OK) =>
        new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public Uri? LastUri;
        public string? LastAuthorization;
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Response = (_,_)=>Task.FromResult(Json("{}"));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls++; LastUri=request.RequestUri; LastAuthorization=request.Headers.Authorization?.ToString();
            return Response(request,ct);
        }
    }
}
