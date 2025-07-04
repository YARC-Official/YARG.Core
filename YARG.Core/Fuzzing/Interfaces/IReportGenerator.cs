using YARG.Core.Fuzzing.Models;

namespace YARG.Core.Fuzzing.Interfaces
{
    /// <summary>
    /// Interface for generating fuzzer test reports.
    /// </summary>
    public interface IReportGenerator
    {
        /// <summary>
        /// Generates a detailed report for a single fuzzer result.
        /// </summary>
        /// <param name="result">Fuzzer result to generate report for</param>
        /// <returns>Detailed report string</returns>
        string GenerateDetailedReport(FuzzerResult result);

        /// <summary>
        /// Generates a summary report for multiple fuzzer results.
        /// </summary>
        /// <param name="results">Array of fuzzer results</param>
        /// <returns>Summary report string</returns>
        string GenerateSummaryReport(FuzzerResult[] results);

        /// <summary>
        /// Generates a report in the specified format.
        /// </summary>
        /// <param name="results">Array of fuzzer results</param>
        /// <param name="format">Report format</param>
        /// <returns>Formatted report string</returns>
        string GenerateReport(FuzzerResult[] results, ReportFormat format);

        /// <summary>
        /// Exports a report to the specified file path.
        /// </summary>
        /// <param name="results">Array of fuzzer results</param>
        /// <param name="filePath">File path to export to</param>
        /// <param name="format">Report format</param>
        void ExportReport(FuzzerResult[] results, string filePath, ReportFormat format);
    }

    /// <summary>
    /// Available report formats.
    /// </summary>
    public enum ReportFormat
    {
        /// <summary>Plain text format</summary>
        Text,
        
        /// <summary>JSON format for machine processing</summary>
        Json,
        
        /// <summary>XML format for structured data</summary>
        Xml,
        
        /// <summary>HTML format for web viewing</summary>
        Html,
        
        /// <summary>CSV format for spreadsheet analysis</summary>
        Csv
    }
}