using Cadastral_Management.Controllers;
using Cadastral_Management.Data;
using Cadastral_Management.Models;
using Cadastral_Management.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CadastralManagementTests
{
    [TestFixture]
    public class CadastralObjectControllerTests
    {
        private ApplicationDbContext _context;
        private Mock<ISessionService> _mockSessionService;
        private CadastralObjectController _controller;

        [SetUp]
        public void Setup()
        {
            // Создаём уникальное имя БД для каждого теста — чтобы избежать конфликтов
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _mockSessionService = new Mock<ISessionService>();

            _controller = new CadastralObjectController(_context, _mockSessionService.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
            _controller?.Dispose();
        }

        // Тест 1: MyCadastralObjects - авторизованный гражданин
        [Test]
        public async Task MyCadastralObjects_WhenUserIsCitizen_ReturnsViewWithObjects()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(true);
            _mockSessionService.Setup(s => s.IsCitizen()).Returns(true);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("1");

            var citizenId = 1;

            // Создаём полного Citizen с обязательными свойствами
            var user = new User
            {
                UserId = 1,
                Login = "testcitizen",
                FullName = "Тестовый Гражданин",
                UserType = "Citizen",
                Email = "test@mail.ru",
                PasswordHash = "hash"
            };

            var owner = new Citizen
            {
                CitizenId = citizenId,
                PassportData = "1234 567890",
                User = user
            };

            var testObjects = new List<CadastralObject>
    {
        new CadastralObject
        {
            CadastralObjectId = 1,
            OwnerId = citizenId,
            CadastralNumber = "77:01:000:1234",
            RegistrationDate = DateTime.Now.AddDays(-10),
            Address = "Test Address 1",
            Area = 100.89m,
            CadastralObjectType = "Земельный участок"
        },
        new CadastralObject
        {
            CadastralObjectId = 2,
            OwnerId = citizenId,
            CadastralNumber = "77:01:000:1245",
            RegistrationDate = DateTime.Now.AddDays(-5),
            Address = "Test Address 2",
            Area = 200.10m,
            CadastralObjectType = "Здание"
        }
    };

            var testExtracts = new List<Extract>
    {
        new Extract
        {
            ExtractId = 1,
            RequestedById = citizenId,
            GenerationDate = DateTime.Now.AddDays(-1),
            FilePath = "wwwroot/uploads/test_file1",
            CadastralObjectId = 1
        }
    };

            _context.Users.Add(user);
            _context.Citizens.Add(owner);
            _context.CadastralObjects.AddRange(testObjects);
            _context.Extracts.AddRange(testExtracts);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MyCadastralObjects();

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            Assert.That(viewResult.ViewData["Extracts"], Is.InstanceOf<List<Extract>>());
            var extracts = viewResult.ViewData["Extracts"] as List<Extract>;
            Assert.That(extracts!.Count, Is.EqualTo(1));

            var model = viewResult.Model as List<CadastralObject>;
            Assert.That(model!.Count, Is.EqualTo(2));
        }

        // Тест 2: MyCadastralObjects - неавторизованный пользователь
        [Test]
        public async Task MyCadastralObjects_WhenUserNotAuthenticated_RedirectsToLogin()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsAuthenticated()).Returns(false);

            // Act
            var result = await _controller.MyCadastralObjects();

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect!.ActionName, Is.EqualTo("Login"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Account"));
        }

        // Тест 3: Create (POST) - успешное создание
        [Test]
        public async Task Create_WithValidData_RedirectsToHome()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(true);
            _mockSessionService.Setup(s => s.IsAdmin()).Returns(false);
            _mockSessionService.Setup(s => s.GetUserId()).Returns("10");

            // Текущий сотрудник (чтобы история сохранилась)
            var employeeUser = new User
            {
                UserId = 10,
                Login = "employee",
                FullName = "Сотрудник",
                UserType = "Employee",
                Email = "emp@mail.ru",
                PasswordHash = "hash",
                CreatedAt = DateTime.Now
            };

            var employee = new Employee
            {
                EmployeeId = 10,
                Department = "Кадастровый отдел"
            };

            _context.Users.Add(employeeUser);
            _context.Employees.Add(employee);

            // Владелец (гражданин)
            var ownerUser = new User
            {
                UserId = 1,
                Login = "ownerlogin",
                FullName = "Владелец Объекта",
                UserType = "Citizen",
                Email = "citizen@mail.ru",
                PasswordHash = "hash",
                CreatedAt = DateTime.Now
            };

            var owner = new Citizen
            {
                CitizenId = 1,
                PassportData = "1234567890",
            };

            _context.Users.Add(ownerUser);
            _context.Citizens.Add(owner);
            await _context.SaveChangesAsync();

            var cadastralNumber = "77:01:001:1268";
            var address = "Москва, ул. Ленина, 1";
            var area = "100.5";
            var objectType = "Земельный участок";
            var registrationDate = DateTime.Today;

            // Act
            var result = await _controller.Create(
                cadastralNumber: cadastralNumber,
                address: address,
                area: area,
                objectType: objectType,
                ownerId: 1,
                registrationDate: registrationDate
                );

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Home"));

            // Проверяем данные (без доступа к cadastralObject из контроллера)
            Assert.That(await _context.CadastralObjects.CountAsync(), Is.EqualTo(1));
            var createdObj = await _context.CadastralObjects.FirstAsync();
            Assert.That(createdObj.CadastralNumber, Is.EqualTo(cadastralNumber));
            Assert.That(createdObj.Area, Is.EqualTo(100.5m));

            Assert.That(await _context.CadastralObjectHistories.CountAsync(), Is.EqualTo(1));
        }

        // Тест 4: Delete - успешное удаление
        [Test]
        public async Task Delete_WithValidId_RedirectsToHome()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(true);

            var objectId = 1;

            var user = new User
            {
                UserId = 1,
                FullName = "Test User",
                Login = "test",
                PasswordHash = "hash",
                Email = "test@test.com",
                UserType = "Citizen"
            };

            var owner = new Citizen
            {
                CitizenId = 1,
                PassportData = "1234567890",
                User = user
            };

            var obj = new CadastralObject
            {
                CadastralObjectId = objectId,
                CadastralNumber = "77:01:001:1223",
                OwnerId = 1,
                Address = "TestAddress",
                CadastralObjectType = "Здание",
                Owner = owner
            };

            _context.Users.Add(user);
            _context.Citizens.Add(owner);
            _context.CadastralObjects.Add(obj);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Delete(objectId);

            // Assert
            Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
            var redirect = result as RedirectToActionResult;
            Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
            Assert.That(redirect.ControllerName, Is.EqualTo("Home"));

            Assert.That(_context.CadastralObjects.Count(), Is.EqualTo(0));
        }

        // Тест 5: Details - успешный просмотр
        [Test]
        public async Task Details_WithValidId_ReturnsViewWithLoadedRelations()
        {
            // Arrange
            _mockSessionService.Setup(s => s.IsEmployee()).Returns(true);

            var user = new User { 
                UserId = 1, 
                FullName = "Иванов Иван" ,
                Login = "test",
                UserType = "Employee",
                Email = "test@mail.ru",
                PasswordHash = "hash"
            };
            var citizen = new Citizen {
                CitizenId = 1,
                PassportData = "1234567890",
                User = user 
            };
            var obj = new CadastralObject
            {
                CadastralObjectId = 1,
                CadastralNumber = "77:01:0001:1230",
                OwnerId = 1,
                Address = "TestAddres",
                CadastralObjectType = "Помещение"
            };

            _context.Users.Add(user);
            _context.Citizens.Add(citizen);
            _context.CadastralObjects.Add(obj);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.Details(1);

            // Assert
            Assert.That(result, Is.InstanceOf<ViewResult>());
            var viewResult = result as ViewResult;
            var model = viewResult!.Model as CadastralObject;
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Owner, Is.Not.Null);
            Assert.That(model.Owner!.User, Is.Not.Null);
            Assert.That(model.Owner.User.FullName, Is.EqualTo("Иванов Иван"));
        }
    }
}