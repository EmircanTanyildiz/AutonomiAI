using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutonomiAI.Data;
using AutonomiAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;

namespace AutonomiAI.Controllers
{
    [Authorize(Roles = "User")]
    public class DatasetsController : Controller
    {
        private readonly AutonomiAIDbContext _context;
        private readonly IConfiguration _config;

        public DatasetsController(AutonomiAIDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<IActionResult> Dashboard()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Datasets)
                .Include(u => u.AIModels)
                .Include(u => u.YapilanTestler)
                .FirstOrDefaultAsync(u => u.Email == email);

            return user == null ? Unauthorized() : View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(List<IFormFile> Dosyalar, string VerisetiAdi, string VeriTipi)
        {
            if (Dosyalar == null || Dosyalar.Count == 0)
                return BadRequest("Dosya yüklenmedi.");

            var email = User.Identity!.Name!;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null) return Unauthorized();

            var dataset = new Dataset
            {
                VerisetiAdi = VerisetiAdi,
                VeriTipi = VeriTipi,
                UserId = user.UserId,
                YuklemeTarihi = DateTime.UtcNow
            };
            _context.Datasets.Add(dataset);
            await _context.SaveChangesAsync();   // VerisetiId oluştu

            // ---------- Geçici klasöre kaydet ----------
            var tempDir = Path.Combine("wwwroot", "temp", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            foreach (var f in Dosyalar)
            {
                var rel = f.FileName.Replace("..", "").Replace(":", "");
                var local = Path.Combine(tempDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(local)!);

                await using var fs = new FileStream(local, FileMode.Create);
                await f.CopyToAsync(fs);
            }

            var host = _config["SshSettings:Host"]!;
            var userSsh = _config["SshSettings:Username"]!;
            var pwdSsh = _config["SshSettings:Password"]!;     
            var basePath = _config["SshSettings:BasePath"] ?? "files";

            using var sftp = new SftpClient(host, userSsh, pwdSsh);
            sftp.Connect();

            var workDir = sftp.WorkingDirectory.TrimEnd('/');
            var remoteRoot = $"{workDir}/{basePath}".TrimEnd('/');
            var remotePath = $"{remoteRoot}/{user.UserId}/{dataset.VerisetiId}";

            EnsureDir(sftp, remotePath);

            //  Dosyaları yükle
            foreach (var local in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(tempDir, local).Replace("\\", "/");
                var rFile = $"{remotePath}/{rel}";
                var rDir = Path.GetDirectoryName(rFile)?.Replace("\\", "/");

                if (!string.IsNullOrWhiteSpace(rDir))
                    EnsureDir(sftp, rDir);

                await using var fs = System.IO.File.OpenRead(local);
                sftp.UploadFile(fs, rFile, true);
            }

            sftp.Disconnect();

            // DB güncelle
            dataset.VeriSetiYolu = remotePath;
            _context.Update(dataset);
            await _context.SaveChangesAsync();

            Directory.Delete(tempDir, true);
            return RedirectToAction(nameof(Dashboard));
        }









        private void EnsureDir(SftpClient sftp, string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var current = "/";
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current.EndsWith("/")
                    ? current + part
                    : current + "/" + part;

                if (!sftp.Exists(current))
                    sftp.CreateDirectory(current);
            }
        }
    }
}
