namespace OEEMonitoringSystem.Services.Interfaces
{
    public class OeeDatasetImportResult
    {
        public int MachinesCreated { get; set; }
        public int EventsImported { get; set; }
        public IList<string> Errors { get; set; } = new List<string>();
    }

    public class OeeReportImportResult
    {
        public int MachinesCreated { get; set; }
        public int CalculationsImported { get; set; }
        public IList<string> Errors { get; set; } = new List<string>();
    }

    public class CombinedImportResult
    {
        public int MachinesCreated { get; set; }
        public int EventsImported { get; set; }
        public int CalculationsImported { get; set; }
        public IList<string> Errors { get; set; } = new List<string>();
    }

    public interface IDatasetImportService
    {
        Task<OeeDatasetImportResult> ImportOEEDatasetAsync(Stream fileStream);
        Task<OeeReportImportResult> ImportOeeReportAsync(Stream fileStream);
        /// <summary>Import both OEE_Dataset and OeeReport in one go; combined data is used for ML.</summary>
        Task<CombinedImportResult> ImportBothAsync(Stream oeeDatasetStream, Stream oeeReportStream);
    }
}
