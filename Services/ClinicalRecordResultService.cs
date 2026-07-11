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
            "đã xử lí";
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
            .Where(x =>
                x.ClinicalRecordId == clinicalRecordId
            )
            .OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Status,
                x.Label,
                x.Confidence
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ultrasoundResult =
            await _context.UltrasoundResults
                .AsNoTracking()
                .Where(x =>
                    x.ClinicalRecordId == clinicalRecordId
                )
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Status,
                    x.Label,
                    x.Confidence
                })
                .FirstOrDefaultAsync(cancellationToken);

        // Phải có đủ cả NCS và siêu âm.
        if (ncsResult == null || ultrasoundResult == null)
        {
            return false;
        }

        // Cả hai phải xử lý hoàn tất.
        if (
            !IsCompletedStatus(ncsResult.Status) ||
            !IsCompletedStatus(ultrasoundResult.Status)
        )
        {
            return false;
        }

        // NCS phải có label hợp lệ và confidence.
        if (
            !IsValidNcsLabel(ncsResult.Label) ||
            !ncsResult.Confidence.HasValue
        )
        {
            return false;
        }

        // Siêu âm phải có CTS hoặc Control và confidence.
        if (
            IsUltrasoundCts(ultrasoundResult.Label) == null ||
            !ultrasoundResult.Confidence.HasValue
        )
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

        var ultrasoundSupport = GetUltrasoundSupport(
            ncsResult.Label!,
            ultrasoundResult.Label!,
            ultrasoundResult.Confidence.Value
        );

        // Label mức độ lấy từ NCS.
        clinicalRecord.Label = ncsResult.Label;

        // NCS chiếm 70%, siêu âm hỗ trợ 30%.
        clinicalRecord.Confidence = Math.Round(
            ncsResult.Confidence.Value * 0.7 +
            ultrasoundSupport * 0.3,
            4
        );

        await _context.SaveChangesAsync(
            cancellationToken
        );

        return true;
    }
}