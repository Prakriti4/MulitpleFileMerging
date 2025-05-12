using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Multiplefileintopdf.Services.Interface;
using Multiplefileintopdf.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Multiplefileintopdf.Controllers
{
    public class FileController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IRecordService _recordService;

        public FileController(IWebHostEnvironment webHostEnvironment, IRecordService recordService)
        {
            _webHostEnvironment = webHostEnvironment;
            _recordService = recordService;
        }

        public async Task<IActionResult> Index()
        {
            var index =await _recordService.GetAll();
            return View(index);
        }

        [HttpPost]
        public IActionResult Upload(List<IFormFile> files)
        {
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");

            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    file.CopyTo(stream);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadFile(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
                return BadRequest("Invalid file path.");

            var path = Path.Combine(_webHostEnvironment.WebRootPath, fileUrl.TrimStart('/'));

            if (!System.IO.File.Exists(path))
                return NotFound("File not found.");

            var fileBytes = System.IO.File.ReadAllBytes(path);
            return File(fileBytes, "application/octet-stream", Path.GetFileName(path));
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRecordVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _recordService.CreateRecord(model);
            return RedirectToAction("Index");
        }
    }
}
