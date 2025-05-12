using Multiplefileintopdf.ViewModel;

namespace Multiplefileintopdf.Services.Interface
{
    public interface IRecordService
    {
        Task<List<GetRecordVM>> GetAll();
        Task<GetRecordVM> GetRecordById(Guid id);
        //Task<GetRecordVM> UpdateRecord(UpdateRecordVM recordVM);
        Task<GetRecordVM> CreateRecord(CreateRecordVM recordVM);
        //Task DeleteRecord(Guid id);
    }
}
