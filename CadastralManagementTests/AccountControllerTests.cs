using Cadastral_Management.Controllers;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Cadastral_Management.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
namespace CadastralManagement.Tests
{
    [TestFixture]
    public class AccountControllerTests
    {
        private Mock<IUserService> _mockUserService;
        private Mock<ISessionService> _mockSessionService;
        private AccountController _controller;
        [SetUp]
        public void Setup()
        {
            _mockUserService = new Mock<IUserService>();
            _mockSessionService = new Mock<ISessionService>();
            _controller = new AccountController(
                _mockUserService.Object,
                _mockSessionService.Object,
                null
            );
        }
        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
        }
        [Test]
        public async Task Login_WithValidCredentials_RedirectsToHomeIndex()
        {
            var testUser = new User
            {
                UserId = 1,
                Login = "testuser",
                PasswordHash = "hashed_password",
                FullName = "Test User",
                UserType = "Citizen"
            };
            _mockUserService
                .Setup(x => x.AuthenticateAsync("testuser", "password123"))
                .ReturnsAsync(testUser);

            var result = await _controller.Login("testuser", "password123");

            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult.ActionName, Is.EqualTo("Index"));
            Assert.That(redirectResult.ControllerName, Is.EqualTo("Home"));

            _mockSessionService.Verify(x => x.SetUserId(1), Times.Once);
            _mockSessionService.Verify(x => x.SetUserName("Test User"), Times.Once);
            _mockSessionService.Verify(x => x.SetUserType("Citizen"), Times.Once);
        }

        [Test]
        public async Task Login_WithInvalidCredentials_ReturnsViewWithError()
        {
            _mockUserService
                .Setup(x => x.AuthenticateAsync("wronguser", "wrongpass"))
                .ReturnsAsync((User)null);

            var result = await _controller.Login("wronguser", "wrongpass");

            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Error"], Is.EqualTo("Неверный логин или пароль"));
        }

        [Test]
        public async Task Login_WhenServiceThrowsException_ReturnsViewWithGenericError()
        {
            _mockUserService
                .Setup(x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _controller.Login("testuser", "password123");

            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Error"], Is.EqualTo("Произошла ошибка при входе. Попробуйте еще раз."));
        }

        [Test]
        public async Task Register_WithNewUser_RedirectsToHomeIndex()
        {
            _mockUserService.Setup(x => x.UserExistsByLoginAsync("newuser")).ReturnsAsync(false);
            _mockUserService.Setup(x => x.UserExistsByEmailAsync("new@email.com")).ReturnsAsync(false);
            _mockUserService.Setup(x => x.CitizenExistsByPassportAsync("1234567890")).ReturnsAsync(false);

            // Ключевой setup с Callback для изменения UserId на переданном user
            _mockUserService
                .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()))
                .Callback<User, string>((u, p) => u.UserId = 1)  // Изменяем UserId на 1
                .Returns<User, string>((u, p) => Task.FromResult(u));  // Возвращаем модифицированный user

            // Мок DbContext
            var citizensData = new List<Citizen>();
            var mockCitizenDbSet = CreateMockDbSet(citizensData);

            var options = new DbContextOptions<ApplicationDbContext>();
            var mockContext = new Mock<ApplicationDbContext>(options);
            mockContext.Setup(c => c.Citizens).Returns(mockCitizenDbSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Пересоздаём контроллер с моками
            _controller = new AccountController(
                _mockUserService.Object,
                _mockSessionService.Object,
                mockContext.Object
            );

            var result = await _controller.Register(
                login: "newuser",
                password: "password123",
                fullName: "New User",
                email: "new@email.com",
                phoneNumber: "+79991234567",
                passportData: "1234567890");

            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult!.ActionName, Is.EqualTo("Index"));
            Assert.That(redirectResult!.ControllerName, Is.EqualTo("Home"));

            // Проверяем вызов CreateUserAsync
            _mockUserService.Verify(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Once);

            // Теперь сессия с UserId = 1
            _mockSessionService.Verify(x => x.SetUserId(1), Times.Once);
            _mockSessionService.Verify(x => x.SetUserName("New User"), Times.Once);
            _mockSessionService.Verify(x => x.SetUserType("Citizen"), Times.Once);

            // Проверяем сохранение Citizen с правильным CitizenId = 1
            mockCitizenDbSet.Verify(x => x.Add(It.Is<Citizen>(c => c.CitizenId == 1 && c.PassportData == "1234567890")), Times.Once);
            mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // Вспомогательный метод для создания мока DbSet
        private static Mock<DbSet<T>> CreateMockDbSet<T>(List<T> data) where T : class
        {
            var queryable = data.AsQueryable();

            var mockDbSet = new Mock<DbSet<T>>();
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());

            mockDbSet.Setup(d => d.Add(It.IsAny<T>())).Callback<T>(data.Add);

            return mockDbSet;
        }

        [Test]
        public async Task Register_WithExistingLogin_ReturnsViewWithError()
        {
            _mockUserService
                .Setup(x => x.UserExistsByLoginAsync("existinguser"))
                .ReturnsAsync(true);

            var result = await _controller.Register(
                login: "existinguser",
                password: "password123",
                fullName: "Existing User",
                email: "test@email.com",
                phoneNumber: "+79991234567",
                passportData: "1234567890");

            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Error"], Is.EqualTo("Пользователь с таким логином уже существует"));

            _mockUserService.Verify(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Logout_CallsClearSessionAndRedirects()
        {
            var result = _controller.Logout();
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirectResult = result as RedirectToActionResult;
            Assert.That(redirectResult.ActionName, Is.EqualTo("Index"));
            Assert.That(redirectResult.ControllerName, Is.EqualTo("Home"));
            _mockSessionService.Verify(x => x.ClearSession(), Times.Once);
        }
    }
}