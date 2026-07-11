using CTS_backend.Data;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Services;

public interface IClinicalRecordResultService
{
    Task<bool> UpdateIfReadyAsync(
        int clinicalRecordId,
        CancellationToken cancellationToken = default
    );
}

public class ClinicalRecordResultService
    : IClinicalRecordResultService
{
    private readonly AppDbContext _context;

    public ClinicalRecordResultService(AppDbContext context)
    {
        _context = context;
    }

    private static bool IsCompletedStatus(string? status)
    {
        var normalized = status?
            .Trim()
            .ToLowerInvariant();

        return normalized is
            "đã xử lý";
    }

    private static string NormalizeLabel(string? label)
    {
        return label?
            .Trim()
            .ToLowerInvariant() ?? string.Empty;
    }

    private static bool IsValidNcsLabel(string? label)
    {
        return NormalizeLabel(label) is
            "bt" or
            "nhe" or
            "tb" or
            "nang";
    }
    private static bool? IsUltrasoundCts(string? label)
    {
        return NormalizeLabel(label) switch
        {
            "cts" => true,
            "control" => false,
            _ => null
        };
    }


    private static double GetUltrasoundSupport(
       string ncsLabel,
       string ultrasoundLabel,
       double ultrasoundConfidence
   )
    {
        var ncsPredictsCts =
            NormalizeLabel(ncsLabel) != "bt";

        var ultrasoundPredictsCts =
            IsUltrasoundCts(ultrasoundLabel);

        if (!ultrasoundPredictsCts.HasValue)
        {
            throw new ArgumentException(
                "Ultrasound label must be CTS or Control."
            );
        }

        var predictionsAgree =
            ncsPredictsCts == ultrasoundPredictsCts.Value;

        return predictionsAgree
            ? ultrasoundConfidence
            : 1.0 - ultrasoundConfidence;
    }

    public async Task<bool> UpdateIfReadyAsync(
    int clinicalRecordId,
    CancellationToken cancellationToken = default
)
    {
        var ncsResult = await _context.NcsResults
            .AsNoTracking()
            .Where(x => x.ClinicalRecordId == clinicalRecordId)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Status,
                x.Label,
                x.Confidence
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ultrasoundResult = await _context.UltrasoundResults
            .AsNoTracking()
            .Where(x => x.ClinicalRecordId == clinicalRecordId)
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Status,
                x.Label,
                x.Confidence
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Không có loại khảo sát nào.
        if (ncsResult == null && ultrasoundResult == null)
        {
            return false;
        }

        /*
         * Nếu có NCS thì NCS đó phải hoàn tất
         * và phải có label, confidence hợp lệ.
         */
        var ncsReady =
            ncsResult != null &&
            IsCompletedStatus(ncsResult.Status) &&
            IsValidNcsLabel(ncsResult.Label) &&
            ncsResult.Confidence.HasValue;

        /*
         * Nếu có siêu âm thì siêu âm đó phải hoàn tất
         * và phải có label, confidence hợp lệ.
         */
        var ultrasoundReady =
            ultrasoundResult != null &&
            IsCompletedStatus(ultrasoundResult.Status) &&
            IsUltrasoundCts(ultrasoundResult.Label) != null &&
            ultrasoundResult.Confidence.HasValue;

        // Có NCS nhưng chưa hoàn tất hoặc thiếu kết quả.
        if (ncsResult != null && !ncsReady)
        {
            return false;
        }

        // Có siêu âm nhưng chưa hoàn tất hoặc thiếu kết quả.
        if (ultrasoundResult != null && !ultrasoundReady)
        {
            return false;
        }

        var clinicalRecord = await _context.ClinicalRecords
            .FirstOrDefaultAsync(
                x => x.Id == clinicalRecordId,
                cancellationToken
            );

        if (clinicalRecord == null)
        {
            return false;
        }

        // Trường hợp 1: Có cả NCS và siêu âm.
        if (ncsReady && ultrasoundReady)
        {
            var ultrasoundSupport = GetUltrasoundSupport(
                ncsResult!.Label!,
                ultrasoundResult!.Label!,
                ultrasoundResult.Confidence!.Value
            );

            // Nhãn mức độ lấy từ NCS.
            clinicalRecord.Label = ncsResult.Label;

            // Confidence tổng hợp.
            clinicalRecord.Confidence = Math.Round(
                ncsResult.Confidence!.Value * 0.7 +
                ultrasoundSupport * 0.3,
                4
            );
        }
        // Trường hợp 2: Chỉ có NCS.
        else if (ncsReady)
        {
            clinicalRecord.Label = ncsResult!.Label;
            clinicalRecord.Confidence =
                Math.Round(ncsResult.Confidence!.Value, 4);
        }
        // Trường hợp 3: Chỉ có siêu âm.
        else if (ultrasoundReady)
        {
            clinicalRecord.Label = ultrasoundResult!.Label;
            clinicalRecord.Confidence =
                Math.Round(ultrasoundResult.Confidence!.Value, 4);
        }
        else
        {
            return false;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}