using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.EntityFrameworkCore;
using Multiplefileintopdf.Data;
using Multiplefileintopdf.Models;
using Multiplefileintopdf.Services.Interface;
using Multiplefileintopdf.ViewModel;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;

namespace Multiplefileintopdf.Services.Implementation
{
    public class RecordService : IRecordService
    {
        private readonly AppDbContext _dbContext;
        private readonly DbSet<Record> _records;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public RecordService(AppDbContext dbContext, IWebHostEnvironment hostingEnvironment)
        {
            _dbContext = dbContext;
            _records = _dbContext.Set<Record>();
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task<List<GetRecordVM>> GetAll()
        {
            return await _records
                .Select(r => new GetRecordVM
                {
                    Id = r.id,
                    DocumentName = r.DocumentName,
                    FileUrl = r.FileUrl
                })
                .ToListAsync();
        }

        public async Task<GetRecordVM> GetRecordById(Guid id)
        {
            var record = await _records.FindAsync(id);
            return record == null ? null : new GetRecordVM
            {
                Id = record.id,
                DocumentName = record.DocumentName,
                FileUrl = record.FileUrl
            };
        }

        public async Task DeleteRecord(Guid id)
        {
            var record = await _records.FindAsync(id);
            if (record != null)
            {
                var filePath = Path.Combine(_hostingEnvironment.WebRootPath, record.FileUrl);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                _records.Remove(record);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<GetRecordVM> CreateRecord(CreateRecordVM recordVM)
        {
            if (recordVM.Files == null || recordVM.Files.Count == 0)
                throw new ArgumentException("At least one file must be uploaded");

            if (recordVM.Files.Count > 10)
                throw new ArgumentException("Too many files uploaded. Maximum 10 allowed");

            if (!recordVM.Files.All(f => f.Length < 5 * 1024 * 1024))
                throw new AggregateException("Each file must be less than 5MB.");

            // Create necessary directories
            var pdfOutputPath = Path.Combine(_hostingEnvironment.WebRootPath, "pdf");
            Directory.CreateDirectory(pdfOutputPath);

            // Generate merged PDF
            string mergedFileName = $"{Guid.NewGuid()}.pdf";
            string mergedFullPath = Path.Combine(pdfOutputPath, mergedFileName);

            await MergeFilesToPdfAsync(recordVM.Files, mergedFullPath);

            // Save record to database
            var record = new Record
            {
                id = Guid.NewGuid(),
                DocumentName = recordVM.DocumentName,
                FileUrl = Path.Combine("pdf", mergedFileName).Replace("\\", "/")
            };

            await _records.AddAsync(record);
            await _dbContext.SaveChangesAsync();

            return new GetRecordVM
            {
                Id = record.id,
                DocumentName = record.DocumentName,
                FileUrl = record.FileUrl
            };
        }
        private async Task MergeFilesToPdfAsync(IEnumerable<IFormFile> files, string outputPath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var outputStream = new FileStream(outputPath, FileMode.Create))
                {
                    using (var document = new Document())
                    {
                        var writer = PdfWriter.GetInstance(document, outputStream);
                        writer.SetFullCompression();
                        writer.CompressionLevel = PdfStream.BEST_COMPRESSION;

                        document.Open();

                        foreach (var file in files)
                        {
                            var extension = Path.GetExtension(file.FileName).ToLower();
                            var tempFilePath = Path.Combine(tempDir, Guid.NewGuid() + extension);

                            // Save the file temporarily
                            using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            try
                            {
                                if (extension == ".pdf")
                                {
                                    // Initialize PdfReader with error handling
                                    PdfReader reader = null;
                                    try
                                    {
                                        reader = new PdfReader(tempFilePath);

                                        // Additional check for encrypted PDFs
                                        if (reader.IsEncrypted())
                                        {
                                            throw new Exception($"PDF file {file.FileName} is password-protected and cannot be processed");
                                        }

                                        for (int i = 1; i <= reader.NumberOfPages; i++)
                                        {
                                            document.NewPage();
                                            var page = writer.GetImportedPage(reader, i);
                                            writer.DirectContent.AddTemplate(page, 0, 0);
                                        }
                                    }
                                    finally
                                    {
                                        reader?.Close();
                                    }
                                }
                                else if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                                {
                                    // Image processing remains the same
                                    var compressedImagePath = Path.Combine(tempDir, "compressed_" + Guid.NewGuid() + ".jpg");

                                    using (var imageInput = file.OpenReadStream())
                                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageInput))
                                    {
                                        image.Mutate(x => x.Resize(new ResizeOptions
                                        {
                                            Mode = ResizeMode.Max,
                                            Size = new Size(1024, 1024)
                                        }));

                                        await image.SaveAsJpegAsync(compressedImagePath, new JpegEncoder
                                        {
                                            Quality = 60
                                        });
                                    }

                                    document.NewPage();
                                    var img = iTextSharp.text.Image.GetInstance(compressedImagePath);
                                    img.ScaleToFit(document.PageSize.Width - document.LeftMargin - document.RightMargin,
                                                 document.PageSize.Height - document.TopMargin - document.BottomMargin);
                                    img.Alignment = Element.ALIGN_CENTER;
                                    document.Add(img);
                                }
                                else
                                {
                                    throw new Exception($"Unsupported file type: {extension}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Clean up the temporary file if something went wrong
                                if (File.Exists(tempFilePath))
                                {
                                    File.Delete(tempFilePath);
                                }
                                throw new Exception($"Error processing file {file.FileName}: {ex.Message}");
                            }
                        }

                        document.Close();
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Silent cleanup - we don't care if temp files aren't deleted
                    }
                }
            }
        }



        public async Task<string> MergeFilesAsync(List<IFormFile> files)
        {
            // Create necessary directories
            var pdfOutputPath = Path.Combine(_hostingEnvironment.WebRootPath, "pdf");
            Directory.CreateDirectory(pdfOutputPath);

            string mergedFileName = $"{Guid.NewGuid()}.pdf";
            string mergedFullPath = Path.Combine(pdfOutputPath, mergedFileName);

            await MergeFilesToPdfAsync(files, mergedFullPath);

            return Path.Combine("pdf", mergedFileName).Replace("\\", "/");
        }
    }
}