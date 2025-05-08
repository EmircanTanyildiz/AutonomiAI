using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutonomiAI.Models;

namespace AutonomiAI.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public AccountController(IHttpClientFactory httpClientFactory,
                                 IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login()
        {
            TempData["appName"] = "AutonomiAI";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(User user)
        {
            TempData["appName"] = "AutonomiAI";

            if (string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                ViewBag.Error = "Please fill in all required fields.";
                return View(user);
            }

            var client = _httpClientFactory.CreateClient();
            var payload = new { email = user.Email, password = user.Password };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8, "application/json");

            var res = await client.PostAsync(
                $"{_config["ApiBaseUrl"]}/api/security/login",
                content);

            if (!res.IsSuccessStatusCode)
            {
                ViewBag.Error = "Email or password is incorrect.";
                return View(user);
            }

            var json = await res.Content.ReadAsStringAsync();
            string token = JsonConvert
                .DeserializeObject<dynamic>(json)!.token.ToString()!;

            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            string role = jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value;

            return RedirectToAction("Dashboard",
                        role == "Admin" ? "Admin" : "User");
        }

        [HttpGet]
        public IActionResult Register()
        {
            TempData["appName"] = "AutonomiAI";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User user, string confirmpassword)
        {
            TempData["appName"] = "AutonomiAI";

            if (string.IsNullOrWhiteSpace(user.Name) ||
                string.IsNullOrWhiteSpace(user.Surname) ||
                string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.Password))
            {
                ViewBag.Error = "Please fill in all required fields.";
                return View(user);
            }

            if (user.Password != confirmpassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View(user);
            }

            if (!user.PrivacyAndTerms)
            {
                ViewBag.Error = "You must accept the privacy policy and terms.";
                return View(user);
            }

            var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                name = user.Name,
                surname = user.Surname,
                email = user.Email,
                password = user.Password,
                privacyAndTerms = user.PrivacyAndTerms
            };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8, "application/json");

            var res = await client.PostAsync(
                $"{_config["ApiBaseUrl"]}/api/security/register",
                content);

            if (!res.IsSuccessStatusCode)
            {
                ViewBag.Error = "This email is already registered.";
                return View(user);
            }

            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt");
            return RedirectToAction("Login");
        }
    }
}
