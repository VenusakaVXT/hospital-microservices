using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Transport;
using ClinicalAPI.Models;

namespace ClinicalAPI.Services;

public class ElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticSearchService> _logger;
    private const string IndexName = "hospital-medical-records";

    public ElasticSearchService(IConfiguration configuration, ILogger<ElasticSearchService> logger)
    {
        _logger = logger;
        
        var elasticUrl = configuration.GetConnectionString("Elasticsearch") ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(elasticUrl))
            .DefaultIndex(IndexName)
            // Tắt xác thực chứng chỉ nếu chạy local test HTTP
            .ServerCertificateValidationCallback(CertificateValidations.AllowAll);

        _client = new ElasticsearchClient(settings);
    }

    /// <summary>
    /// Khởi tạo Index với Custom Analyzer để hỗ trợ tìm kiếm Tiếng Việt không dấu / gần đúng.
    /// Hàm này nên được gọi lúc khởi động ứng dụng (ví dụ trong Program.cs) hoặc lazy load.
    /// </summary>
    public async Task CreateIndexIfNotExistsAsync()
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName);
        if (!existsResponse.Exists)
        {
            var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .Analysis(a => a
                        .Analyzers(analyzers => analyzers
                            .Custom("vietnamese_ascii", ca => ca
                                .Tokenizer("standard")
                                .Filter(new[] { "lowercase", "asciifolding" })
                            )
                        )
                    )
                )
                .Mappings(m => m
                    .Properties<MedicalRecord>(p => p
                        .Text(t => t.Diagnosis, tc => tc.Analyzer("vietnamese_ascii"))
                        .Text(t => t.Symptoms, tc => tc.Analyzer("vietnamese_ascii"))
                        .Text(t => t.Treatment, tc => tc.Analyzer("vietnamese_ascii"))
                        .Text(t => t.Notes, tc => tc.Analyzer("vietnamese_ascii"))
                        .Keyword(k => k.PatientName)
                        .Keyword(k => k.DoctorName)
                        .Keyword(k => k.Status)
                    )
                )
            );

            if (createResponse.IsValidResponse)
                _logger.LogInformation("✅ [Elasticsearch] Khởi tạo Index '{Index}' thành công.", IndexName);
            else
                _logger.LogError("❌ [Elasticsearch] Lỗi khởi tạo Index: {Error}", createResponse.DebugInformation);
        }
    }

    /// <summary>
    /// Đồng bộ một bệnh án lên Elasticsearch.
    /// Được gọi mỗi khi DB có thêm/sửa bệnh án.
    /// </summary>
    public async Task IndexMedicalRecordAsync(MedicalRecord record)
    {
        try
        {
            var response = await _client.IndexAsync(record, idx => idx.Index(IndexName).Id(record.Id.ToString()));
            
            if (response.IsValidResponse)
                _logger.LogInformation("✅ [Elasticsearch] Đã index MedicalRecord ID {Id}", record.Id);
            else
                _logger.LogWarning("⚠️ [Elasticsearch] Không thể index MedicalRecord ID {Id}. Lỗi: {Error}", record.Id, response.DebugInformation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Elasticsearch] Lỗi hệ thống khi index MedicalRecord ID {Id}", record.Id);
        }
    }
    
    /// <summary>
    /// Xóa một bệnh án khỏi Elasticsearch.
    /// </summary>
    public async Task DeleteMedicalRecordAsync(Guid id)
    {
        try
        {
            var response = await _client.DeleteAsync<MedicalRecord>(id.ToString(), idx => idx.Index(IndexName));
            if (response.IsValidResponse)
                _logger.LogInformation("✅ [Elasticsearch] Đã xóa MedicalRecord ID {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Elasticsearch] Lỗi xóa MedicalRecord ID {Id}", id);
        }
    }

    /// <summary>
    /// Tìm kiếm Full-text (MultiMatch) trên các trường văn bản.
    /// </summary>
    public async Task<List<MedicalRecord>> SearchMedicalRecordsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new List<MedicalRecord>();

        try
        {
            var response = await _client.SearchAsync<MedicalRecord>(s => s
                .Index(IndexName)
                .Query(q => q
                    .MultiMatch(m => m
                        .Query(keyword)
                        .Fields(new[] { 
                            "diagnosis^3", // Boost x3 cho diagnosis
                            "symptoms^2",  // Boost x2 cho symptoms
                            "treatment",
                            "notes" 
                        })
                        // Operator AND hoặc OR. Mặc định là OR.
                        // Có thể dùng Fuzziness(new Fuzziness(1)) để sai lỗi chính tả.
                    )
                )
            );

            if (response.IsValidResponse)
            {
                _logger.LogInformation("🔍 [Elasticsearch] Tìm thấy {Count} kết quả cho từ khóa '{Keyword}'", response.Documents.Count, keyword);
                return response.Documents.ToList();
            }
            
            _logger.LogWarning("⚠️ [Elasticsearch] Lỗi truy vấn: {Error}", response.DebugInformation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [Elasticsearch] Lỗi hệ thống khi tìm kiếm.");
        }

        return new List<MedicalRecord>();
    }
}
