using CurrencyConverter.API.Controllers;
using CurrencyConverter.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CurrencyConverter.API.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly IConfiguration _mockConfig;

        public AuthControllerTests()
        {
            var inMemorySettings = new Dictionary<string, string> {
                {"Jwt:Key", "aP3x9mT6rYQ1wU8jK2eZ5nH7pR4sV0cX"}
            };

            _mockConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Theory]
        [InlineData("admin", "password")]
        [InlineData("operator", "password")]
        public void Login_Should_Return_Token_When_Credentials_Valid(string username, string password)
        {
            var controller = new AuthController(_mockConfig);
            var request = new UserLoginRequest { Username = username, Password = password };

            var result = controller.Login(request) as OkObjectResult;

            result.Should().NotBeNull();
            result!.StatusCode.Should().Be(200);

            var token = result.Value?.GetType().GetProperty("token")?.GetValue(result.Value)?.ToString();
            token.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData("admin", "wrongpassword")]
        [InlineData("unknown", "password")]
        [InlineData("", "")]
        public void Login_Should_Return_Unauthorized_When_Credentials_Invalid(string username, string password)
        {
            var controller = new AuthController(_mockConfig);
            var request = new UserLoginRequest { Username = username, Password = password };

            var result = controller.Login(request);

            result.Should().BeOfType<UnauthorizedResult>();
        }
    }
}
