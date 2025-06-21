using CurrencyConverter.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyConverter.API.Tests.Middleware
{
    public class RequestLoggingMiddlewareTests
    {
        [Fact]
        public async Task Should_Log_Request_With_Correct_Details()
        {
            var loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
            var context = new DefaultHttpContext();

            context.Request.Method = "GET";
            context.Request.Path = "/api/test";
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            var claims = new[] { new Claim("client_id", "abc123") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            context.User = new ClaimsPrincipal(identity);

            var wasNextCalled = false;
            RequestDelegate next = ctx =>
            {
                wasNextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new RequestLoggingMiddleware(next, loggerMock.Object);

            await middleware.Invoke(context);

            wasNextCalled.Should().BeTrue();

            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString().Contains("GET") &&
                        v.ToString().Contains("/api/test") &&
                        v.ToString().Contains("200") &&
                        v.ToString().Contains("127.0.0.1") &&
                        v.ToString().Contains("abc123")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()),
                Times.Once);
        }
    }
}
