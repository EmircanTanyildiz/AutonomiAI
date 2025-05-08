using AutonomiAI.Data;
using AutonomiAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace AutonomiAI.Controllers
{
    [Authorize(Roles = "User")]
    public class Testler : Controller
    {
        private readonly AutonomiAIDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public Testler(AutonomiAIDbContext context, IConfiguration config, IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _config = config;
            _env = env;
            _httpClientFactory = httpClientFactory;
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
        public async Task<IActionResult> TestImageUpload(IFormFile TestImage, int ModelId)
        {
            if (TestImage == null || TestImage.Length == 0)
                return Json(new { success = false, message = "Test görseli yüklenmedi." });

            var email = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return Unauthorized(new { success = false, message = "Kullanıcı bulunamadı." });

            int userId = user.UserId;

            var model = await _context.AIModels.FirstOrDefaultAsync(m => m.ModelId == ModelId && m.UserId == userId);
            if (model == null)
                return Json(new { success = false, message = "Model bulunamadı." });

            var dataset = await _context.Datasets.FirstOrDefaultAsync(d => d.VerisetiId == model.DatasetID);
            if (dataset == null)
                return Json(new { success = false, message = "Veri seti bulunamadı." });

            // SSH Ayarları
            string sshHost = _config["SshSettings:Host"]!;
            string sshUsername = _config["SshSettings:Username"]!;
            string sshPassword = _config["SshSettings:Password"]!;
            string veriSetiYolu = dataset.VeriSetiYolu;

            string class1 = "", class2 = "";

            try
            {
                using (var sftp = new Renci.SshNet.SftpClient(sshHost, sshUsername, sshPassword))
                {
                    sftp.Connect();

                    var anaKlasorler = sftp.ListDirectory(veriSetiYolu)
                        .Where(x => x.IsDirectory && x.Name != "." && x.Name != "..")
                        .ToList();

                    if (anaKlasorler.Count != 1)
                        return Json(new { success = false, message = "Ana dizinde yalnızca bir klasör (örneğin Train) olmalıdır." });

                    string altKlasorPath = veriSetiYolu + "/" + anaKlasorler.First().Name;

                    var siniflar = sftp.ListDirectory(altKlasorPath)
                        .Where(x => x.IsDirectory && x.Name != "." && x.Name != "..")
                        .Select(x => x.Name)
                        .OrderBy(x => x)
                        .ToList();

                    if (siniflar.Count != 2)
                        return Json(new { success = false, message = "Alt dizinde tam olarak iki sınıf klasörü bulunmalıdır." });

                    class1 = siniflar[0];
                    class2 = siniflar[1];

                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"SFTP klasör listesi hatası: {ex.Message}" });
            }

            int testId = (_context.YapilanTestler.Max(t => (int?)t.TestId) ?? 0) + 1;

            string remoteImageFolder = $"/root/TestImage/{userId}/{ModelId}/{testId}";
            string fileName = Path.GetFileNameWithoutExtension(TestImage.FileName);
            string safeName = Regex.Replace(fileName, "[^a-zA-Z0-9_-]", "_") + Path.GetExtension(TestImage.FileName);
            string remoteImagePath = $"{remoteImageFolder}/{safeName}";

            try
            {
                using var sftp = new Renci.SshNet.SftpClient(sshHost, sshUsername, sshPassword);
                sftp.Connect();

                // remoteImageFolder = "/root/TestImage/{userId}/{ModelId}/{testId}"
                var parts = remoteImageFolder.Trim('/').Split('/');
                string currentPath = "";
                foreach (var part in parts)
                {
                    currentPath += "/" + part;
                    if (!sftp.Exists(currentPath))
                        sftp.CreateDirectory(currentPath);
                }

                // Dosyayı stream üzerinden yükle
                using var ms = new MemoryStream();
                await TestImage.CopyToAsync(ms);
                ms.Position = 0;

                sftp.UploadFile(ms, remoteImagePath);
                sftp.Disconnect();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Görsel yükleme hatası: {ex.Message}"
                });
            }

            var payload = new
            {
                UserId = userId,
                model_path = $"/root/pythonAPI/models/{userId}/{ModelId}.h5",
                imagePath = remoteImagePath,
                class1_Name = class1,
                class2_Name = class2,
                TestID = testId,
                ModelID = ModelId
            };

            //API çağrısı
            string responseJson;
            HttpResponseMessage response;
            try
            {
                var client = _httpClientFactory.CreateClient();
                var requestContent = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                response = await client.PostAsync("http://95.111.254.37:8000/predict", requestContent);
                responseJson = await response.Content.ReadAsStringAsync();

                Console.WriteLine("=== Predict API Response ===");
                Console.WriteLine(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API HATA] {ex}");
                return Json(new
                {
                    success = false,
                    message = $"API isteği hatası: {ex.Message}"
                });
            }

            Dictionary<string, object> resultDict;
            try
            {
                resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PARSE HATA] {ex}");
                return Json(new
                {
                    success = false,
                    message = "Predict API yanıtı parse edilemedi."
                });
            }

            if (!response.IsSuccessStatusCode)
            {
                var hataMesaji = resultDict.ContainsKey("hata")
                    ? resultDict["hata"]?.ToString()
                    : $"Predict API HTTP {(int)response.StatusCode}";
                return Json(new
                {
                    success = false,
                    message = hataMesaji
                });
            }

            string labelName = resultDict.TryGetValue("label_name", out var ln) ? ln?.ToString() ?? "" : "";
            double probability = 0.0;
            if (resultDict.TryGetValue("probability", out var probObj))
                double.TryParse(probObj?.ToString(), out probability);

            string finalClass = probability < 0.5
                ? class1     
                : class2;     
            try
            {
                var yeniTest = new YapilanTest
                {
                    // TestId = testId,

                    UserId = userId,
                    ModelId = ModelId,
                    TestEdilenDataPath = remoteImagePath,
                    Sonuc = finalClass,
                    Zaman = DateTime.UtcNow
                };

                _context.YapilanTestler.Add(yeniTest);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    sonuc = finalClass,
                    probability
                });
            }
            catch (DbUpdateException dbEx)
            {
                var inner = dbEx.GetBaseException().Message;
                Console.WriteLine($"[DB INNER ERROR] {inner}");

                return Json(new
                {
                    success = false,
                    message = $"Veritabanı kayıt hatası: {inner}"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB GENEL HATA] {ex}");
                return Json(new
                {
                    success = false,
                    message = $"Beklenmeyen hata: {ex.Message}"
                });
            }
        }
    }
}



    


