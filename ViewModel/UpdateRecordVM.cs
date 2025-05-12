namespace Multiplefileintopdf.ViewModel
{
    public class UpdateRecordVM
    {
        public Guid Id { get; set; }
        public string DocumentName { get; set; }
        public IFormFileCollection Files { get; set; }
        public string FileUrl {  get; set; }
    }
}
