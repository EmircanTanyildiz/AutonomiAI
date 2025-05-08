using AutonomiAI.Data;
using AutonomiAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using System.Text;
using System.Text.Json;

namespace AutonomiAI.Controllers
{
    [Authorize(Roles = "User")]
    public class Modeller : Controller
    {
        private readonly AutonomiAIDbContext _context;
        private readonly IConfiguration _config;

        public Modeller(AutonomiAIDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<IActionResult> Index()
        {
            var email = User.Identity?.Name;
            var user = await _context.Users
                .Include(u => u.AIModels)
                .Include(u => u.Datasets)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return Unauthorized();

            ViewBag.Datasets = user.Datasets;
            return View(user.AIModels);
        }

        [HttpPost]
        public async Task<IActionResult> YeniModelOlustur(string modelAdi, int datasetId)
        {
            var email = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return Unauthorized();

            var model = new AIModel
            {
                ModelAdi = modelAdi,
                DatasetID = datasetId,
                UserId = user.UserId,
                AlgoritmaTuru = "Hazırlanıyor",
                OlusturmaTarihi = DateTime.UtcNow
            };

            _context.AIModels.Add(model);
            await _context.SaveChangesAsync();

            string sshHost = _config["SshSettings:Host"]!;
            string sshUsername = _config["SshSettings:Username"]!;
            string sshPassword = _config["SshSettings:Password"]!;
            string datasetPath = $"/root/files/{user.UserId}/{datasetId}";
            string? fullSubdirPath = null;

            using (var sftp = new SftpClient(sshHost, sshUsername, sshPassword))
            {
                sftp.Connect();
                var dirs = sftp.ListDirectory(datasetPath)
                               .Where(f => f.IsDirectory && f.Name != "." && f.Name != "..")
                               .ToList();

                if (dirs.Count == 1)
                {
                    fullSubdirPath = $"{datasetPath}/{dirs[0].Name}";
                }
                sftp.Disconnect();
            }

            if (fullSubdirPath == null)
            {
                return BadRequest("Dataset klasörünün içinde tam olarak bir alt klasör bulunmalıdır.");
            }

            var jsonData = new
            {
                UserId = user.UserId,
                ModelId = model.ModelId,
                DatasetPath = fullSubdirPath
            };

            using var httpClient = new HttpClient();
            var content = new StringContent(JsonSerializer.Serialize(jsonData), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("http://95.111.254.37:5000/train", content);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Model eğitim API'sine bağlanılamadı.");
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> GuncelleTumu()
        {
            try
            {
                var email = User.Identity?.Name;
                var user = await _context.Users.Include(u => u.AIModels).FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                    return Json(new { success = false, message = "Kullanıcı bulunamadı." });

                string sshHost = _config["SshSettings:Host"]!;
                string sshUsername = _config["SshSettings:Username"]!;
                string sshPassword = _config["SshSettings:Password"]!;

                using var sftp = new SftpClient(sshHost, sshUsername, sshPassword);
                sftp.Connect();

                foreach (var model in user.AIModels)
                {
                    string resultPath = $"/root/pythonAPI/results/{user.UserId}/{model.ModelId}.json";
                    if (!sftp.Exists(resultPath))
                        continue;

                    using var stream = sftp.OpenRead(resultPath);
                    using var reader = new StreamReader(stream);
                    var jsonContent = await reader.ReadToEndAsync();

                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;

                    model.AlgoritmaTuru = root.GetProperty("Model").GetString() ?? "Bilinmiyor";
                    model.Dogruluk = root.GetProperty("Doğruluk").GetSingle();
                    model.Kayip = root.GetProperty("Kayıp").GetSingle();
                    model.Auc = root.GetProperty("AUC").GetSingle();
                    model.F1Skoru = root.GetProperty("F1 Skoru").GetSingle();
                    model.EgitimSuresi = root.GetProperty("Eğitim Süresi").GetSingle();
                    model.Rocegrisi = root.GetProperty("ROC Eğrisi").GetString();

                    _context.AIModels.Update(model);
                }

                await _context.SaveChangesAsync();
                sftp.Disconnect();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
