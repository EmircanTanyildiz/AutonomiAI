using AutonomiAI.Data;
using AutonomiAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutonomiAI.Controllers
{
    [Authorize(Roles = "User")]
    public class UserController : Controller
    {
        private readonly AutonomiAIDbContext _context;

        public UserController(AutonomiAIDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Datasets)
                .Include(u => u.AIModels)
                .Include(u => u.YapilanTestler)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return Unauthorized();

            return View(user);
        }
    }
}
