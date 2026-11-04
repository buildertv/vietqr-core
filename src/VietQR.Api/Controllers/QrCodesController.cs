using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietQR.Api.Models;
using VietQR.Core;

namespace VietQR.Api.Controllers;

/// <summary>
/// API sinh chuỗi, render ảnh thẻ và giải mã thanh toán VietQR chuẩn NAPAS247 &amp; EMVCo.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/vietqr-core")]
[Produces("application/json")]
public class QrCodesController : ControllerBase
{
    private readonly IBankCatalog bankCatalog;
    private readonly QrImageService qrService;
    private readonly ILogger<QrCodesController> logger;

    public QrCodesController(
        IBankCatalog bankCatalog,
        QrImageService qrService,
        ILogger<QrCodesController> logger)
    {
        this.bankCatalog = bankCatalog;
        this.qrService = qrService;
        this.logger = logger;
    }

    /// <summary>
    /// Tạo mã VietQR và ảnh thẻ thanh toán qua tham số URL (GET).
    /// </summary>
    /// <remarks>
    /// Trả về `qrCode` (chuỗi EMVCo kèm CRC16 Tag 63 - Mục 5.2.15 NAPAS) và `qrDataURL`
    /// (ảnh thẻ render theo template, dạng Base64 Data URL gắn thẳng vào &lt;img&gt;).
    /// </remarks>
    /// <param name="bankBin">Mã BIN ngân hàng thụ hưởng gồm đúng 6 chữ số (Mục 5.2.3.2 NAPAS).</param>
    /// <param name="accountNo">Số tài khoản, số thẻ hoặc tài khoản định danh (tối đa 25 ký tự).</param>
    /// <param name="amount">Số tiền VNĐ, số nguyên dương tối đa 13 chữ số (Tag 54).</param>
    /// <param name="description">Nội dung thanh toán, tối đa 95 ký tự (Tag 62.08).</param>
    /// <param name="accountName">Tên chủ tài khoản hiển thị trên mẫu thẻ (tùy chọn).</param>
    /// <param name="template">Mẫu thẻ: compact (mặc định), qr_only hoặc print.</param>
    /// <param name="serviceType">account - QRIBFTTA (mặc định) hoặc card - QRIBFTTC.</param>
    /// <param name="billNumber">Số hóa đơn, tối đa 25 ký tự (Tag 62.01, tùy chọn).</param>
    /// <param name="format">Định dạng ảnh: png (mặc định), webp hoặc svg.</param>
    /// <param name="size">Kích thước px của mẫu qr_only, trong khoảng 100-2000 (mặc định 500).</param>
    /// <param name="customLogo">Logo đặt ở tâm mã QR dạng Base64 hoặc Data URL (tùy chọn).</param>
    /// <param name="ct">Token hủy tác vụ khi client ngắt kết nối.</param>
    /// <response code="200">Tạo mã thành công (qrCode + qrDataURL).</response>
    /// <response code="400">Tham số không hợp lệ (mã lỗi 01, 02, 03, 04, 05).</response>
    /// <response code="500">Lỗi xử lý trên máy chủ (mã lỗi 99).</response>
    [HttpGet("generate")]
    [EnableRateLimiting("generate")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" })]
    [ProducesResponseType(typeof(ApiResponse<VietQrGenerateResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateVietQrCard(
        [FromQuery] string? bankBin = null,
        [FromQuery] string? accountNo = null,
        [FromQuery] string? amount = null,
        [FromQuery] string? description = null,
        [FromQuery] string? accountName = null,
        [FromQuery] string? template = "compact",
        [FromQuery] string? serviceType = null,
        [FromQuery] string? billNumber = null,
        [FromQuery] string? format = "png",
        [FromQuery] int? size = 500,
        [FromQuery] string? customLogo = null,
        CancellationToken ct = default)
    {
        var req = new VietQrGenerateRequest
        {
            BankBin = bankBin ?? string.Empty,
            AccountNo = accountNo ?? string.Empty,
            Amount = amount ?? string.Empty,
            Description = description ?? string.Empty,
            AccountName = accountName,
            Template = template,
            ServiceType = serviceType,
            BillNumber = billNumber,
            Format = format,
            Size = size,
            CustomLogoBase64 = customLogo
        };

        return await ProcessGenerateAsync(req, ct);
    }

    /// <summary>
    /// Tạo mã VietQR và ảnh thẻ thanh toán qua JSON body (POST).
    /// </summary>
    /// <remarks>
    /// Cùng kết quả với GET nhưng dữ liệu không lộ trong URL, phù hợp khi gửi
    /// `customLogoBase64` kích thước lớn.
    /// </remarks>
    /// <param name="req">Yêu cầu sinh mã VietQR (xem model VietQrGenerateRequest).</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <response code="200">Tạo mã thành công (qrCode + qrDataURL).</response>
    /// <response code="400">Dữ liệu yêu cầu không hợp lệ.</response>
    /// <response code="500">Lỗi xử lý trên máy chủ.</response>
    [HttpPost("generate")]
    [EnableRateLimiting("generate")]
    [ProducesResponseType(typeof(ApiResponse<VietQrGenerateResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateVietQrPost(
        [FromBody] VietQrGenerateRequest req,
        CancellationToken ct = default)
    {
        return await ProcessGenerateAsync(req, ct);
    }

    /// <summary>
    /// Xuất file ảnh mã QR thanh toán trực tiếp qua tham số URL (GET).
    /// </summary>
    /// <remarks>
    /// Trả về binary stream với Content-Type tương ứng (image/png, image/webp, image/svg+xml),
    /// nhúng được ngay vào &lt;img src&gt;, không cần gọi /generate trước.
    /// </remarks>
    /// <param name="bankBin">Mã BIN ngân hàng thụ hưởng gồm đúng 6 chữ số (Mục 5.2.3.2 NAPAS).</param>
    /// <param name="accountNo">Số tài khoản, số thẻ hoặc tài khoản định danh (tối đa 25 ký tự).</param>
    /// <param name="amount">Số tiền VNĐ, số nguyên dương tối đa 13 chữ số (Tag 54).</param>
    /// <param name="description">Nội dung thanh toán, tối đa 95 ký tự (Tag 62.08).</param>
    /// <param name="accountName">Tên chủ tài khoản hiển thị trên mẫu thẻ (tùy chọn).</param>
    /// <param name="template">Mẫu thẻ: compact (mặc định), qr_only hoặc print.</param>
    /// <param name="serviceType">account - QRIBFTTA (mặc định) hoặc card - QRIBFTTC.</param>
    /// <param name="billNumber">Số hóa đơn, tối đa 25 ký tự (Tag 62.01, tùy chọn).</param>
    /// <param name="format">Định dạng file: png (mặc định), webp hoặc svg.</param>
    /// <param name="size">Kích thước px của mẫu qr_only, trong khoảng 100-2000 (mặc định 500).</param>
    /// <param name="customLogo">Logo đặt ở tâm mã QR dạng Base64 hoặc Data URL (tùy chọn).</param>
    /// <param name="ct">Token hủy tác vụ khi client ngắt kết nối.</param>
    /// <response code="200">Trả về stream ảnh (image/png, image/webp hoặc image/svg+xml).</response>
    /// <response code="400">Tham số không hợp lệ.</response>
    [HttpGet("image")]
    [EnableRateLimiting("generate")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" })]
    [Produces("image/png", "image/webp", "image/svg+xml")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQrImage(
        [FromQuery] string? bankBin = null,
        [FromQuery] string? accountNo = null,
        [FromQuery] string? amount = null,
        [FromQuery] string? description = null,
        [FromQuery] string? accountName = null,
        [FromQuery] string? template = "compact",
        [FromQuery] string? serviceType = null,
        [FromQuery] string? billNumber = null,
        [FromQuery] string? format = "png",
        [FromQuery] int? size = 500,
        [FromQuery] string? customLogo = null,
        CancellationToken ct = default)
    {
        var req = new VietQrGenerateRequest
        {
            BankBin = bankBin ?? string.Empty,
            AccountNo = accountNo ?? string.Empty,
            Amount = amount ?? string.Empty,
            Description = description ?? string.Empty,
            AccountName = accountName,
            Template = template,
            ServiceType = serviceType,
            BillNumber = billNumber,
            Format = format,
            Size = size,
            CustomLogoBase64 = customLogo
        };

        return await ProcessImageStreamAsync(req, ct);
    }

    /// <summary>
    /// Xuất file ảnh mã QR thanh toán trực tiếp qua JSON body (POST).
    /// </summary>
    /// <param name="req">Yêu cầu sinh mã VietQR (xem model VietQrGenerateRequest).</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <response code="200">Trả về stream ảnh (image/png, image/webp hoặc image/svg+xml).</response>
    /// <response code="400">Dữ liệu yêu cầu không hợp lệ.</response>
    [HttpPost("image")]
    [EnableRateLimiting("generate")]
    [Produces("image/png", "image/webp", "image/svg+xml")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostQrImage(
        [FromBody] VietQrGenerateRequest req,
        CancellationToken ct = default)
    {
        return await ProcessImageStreamAsync(req, ct);
    }

    /// <summary>
    /// Giải mã hình ảnh QR thành thông tin thanh toán VietQR (NAPAS247).
    /// </summary>
    /// <remarks>
    /// Nhận ảnh qua 2 cách: multipart/form-data (trường `file`) hoặc application/json
    /// (trường `imageBase64`, Base64 thuần hoặc Data URL).
    /// </remarks>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <response code="200">Giải mã thành công, trả về đối tượng VietQrInfo.</response>
    /// <response code="400">Ảnh không hợp lệ hoặc không chứa mã VietQR (mã lỗi 01, 06).</response>
    /// <response code="500">Lỗi máy chủ trong quá trình xử lý ảnh (mã lỗi 99).</response>
    [HttpPost("decode")]
    [Consumes("multipart/form-data", "application/json")]
    [ProducesResponseType(typeof(ApiResponse<VietQrInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DecodeQr(CancellationToken ct = default)
    {
        try
        {
            byte[]? imageBytes = null;

            if (Request.HasFormContentType)
            {
                // Trường "file" trong form-data
                var file = Request.Form.Files.GetFile("file") ?? Request.Form.Files.FirstOrDefault();
                if (file != null && file.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms, ct);
                    imageBytes = ms.ToArray();
                }
            }
            else
            {
                // Trường "imageBase64" trong JSON body (Base64 thuần hoặc Data URL)
                var body = await Request.ReadFromJsonAsync<VietQrDecodeRequest>(ct);
                if (body != null && !string.IsNullOrWhiteSpace(body.ImageBase64))
                {
                    var base64 = body.ImageBase64.Trim();
                    if (base64.Contains(','))
                    {
                        base64 = base64.Substring(base64.IndexOf(',') + 1);
                    }
                    imageBytes = Convert.FromBase64String(base64);
                }
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Error("01", "Vui lòng upload file ảnh qua form-data hoặc gửi trường 'imageBase64' trong JSON body."));
            }

            var decodeResult = VietQrDecoder.Decode(imageBytes);
            if (!decodeResult.IsSuccess || decodeResult.Value == null)
            {
                return BadRequest(ApiResponse<object>.Error("06", $"Không thể giải mã hoặc ảnh không chứa mã VietQR chuẩn. Lỗi: {decodeResult.Error}"));
            }

            return Ok(ApiResponse<VietQrInfo>.Success(decodeResult.Value, "Giải mã thành công"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi giải mã ảnh VietQR.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Error("99", $"Lỗi giải mã: {ex.Message}"));
        }
    }

    /// <summary>
    /// Phân tích chuỗi EMVCo thành đối tượng VietQrInfo có cấu trúc.
    /// </summary>
    /// <remarks>
    /// Bóc tách Tag 38 (người thụ hưởng - Mục 5.2.3.2 NAPAS), Tag 54 (số tiền),
    /// Tag 62 (dữ liệu bổ sung - Mục 5.2.14) và xác thực CRC16 Tag 63 (Mục 5.2.15).
    /// Chuỗi không hợp lệ hoặc CRC sai trả về mã lỗi 07.
    /// </remarks>
    /// <param name="req">Yêu cầu chứa trường qrCode (chuỗi EMVCo thô).</param>
    /// <response code="200">Phân tích thành công, trả về đối tượng VietQrInfo.</response>
    /// <response code="400">Chuỗi rỗng (mã lỗi 01) hoặc sai chuẩn EMVCo / CRC (mã lỗi 07).</response>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(ApiResponse<VietQrInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult ParseQr([FromBody] VietQrParseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.QrCode))
        {
            return BadRequest(ApiResponse<object>.Error("01", "Chuỗi mã 'qrCode' không được để trống."));
        }

        var parseResult = VietQrParser.Parse(req.QrCode.Trim());
        if (!parseResult.IsSuccess || parseResult.Value == null)
        {
            return BadRequest(ApiResponse<object>.Error("07", $"Chuỗi mã không hợp lệ theo chuẩn VietQR / EMVCo. Lỗi: {parseResult.Error}"));
        }

        return Ok(ApiResponse<VietQrInfo>.Success(parseResult.Value, "Phân tích cú pháp thành công"));
    }

    #region Private Pipeline Helpers

    private async Task<IActionResult> ProcessGenerateAsync(VietQrGenerateRequest req, CancellationToken ct)
    {
        try
        {
            var (payload, options, formatStr, errorResult) = await ValidateAndPrepareAsync(req);
            if (errorResult != null) return errorResult;

            string dataUrl;
            if (options.Format == QrImageFormat.Svg)
            {
                var svgXml = await qrService.RenderSvgAsync(payload, options, ct);
                var svgBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svgXml));
                dataUrl = $"data:image/svg+xml;base64,{svgBase64}";
            }
            else
            {
                var bytes = await qrService.RenderBytesAsync(payload, options, ct);
                var mime = options.Format == QrImageFormat.Webp ? "image/webp" : "image/png";
                dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }

            return Ok(ApiResponse<VietQrGenerateResult>.Success(new VietQrGenerateResult
            {
                QrCode = payload,
                QrDataURL = dataUrl
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi tạo mã VietQR.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Error("99", "Đã xảy ra lỗi trong quá trình tạo mã VietQR trên máy chủ."));
        }
    }

    private async Task<IActionResult> ProcessImageStreamAsync(VietQrGenerateRequest req, CancellationToken ct)
    {
        try
        {
            var (payload, options, formatStr, errorResult) = await ValidateAndPrepareAsync(req);
            if (errorResult != null) return errorResult;

            if (options.Format == QrImageFormat.Svg)
            {
                var svgXml = await qrService.RenderSvgAsync(payload, options, ct);
                return Content(svgXml, "image/svg+xml", Encoding.UTF8);
            }

            var bytes = await qrService.RenderBytesAsync(payload, options, ct);
            var mime = options.Format == QrImageFormat.Webp ? "image/webp" : "image/png";
            return File(bytes, mime, $"vietqr-{req.BankBin}-{req.AccountNo}.{formatStr}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi xuất ảnh trực tiếp VietQR.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Error("99", "Đã xảy ra lỗi trong quá trình render ảnh mã VietQR."));
        }
    }

    private async Task<(string Payload, QrRenderOptions Options, string FormatStr, IActionResult? ErrorResult)> ValidateAndPrepareAsync(VietQrGenerateRequest req)
{
    var missingParams = new List<string>();

    if (string.IsNullOrWhiteSpace(req.BankBin)) missingParams.Add("bankBin");
    if (string.IsNullOrWhiteSpace(req.AccountNo)) missingParams.Add("accountNo");
    if (string.IsNullOrWhiteSpace(req.Description)) missingParams.Add("description");

    if (missingParams.Count > 0)
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("01",
            $"Thiếu tham số bắt buộc: {string.Join(", ", missingParams)}. Yêu cầu: bankBin, accountNo, description.")));
    }

    // Amount là tùy chọn
    long? val = null;

    if (!string.IsNullOrWhiteSpace(req.Amount))
    {
        var trimmedAmount = req.Amount.Trim();

        if (!trimmedAmount.All(c => c >= '0' && c <= '9')
            || trimmedAmount.Length > 13
            || !long.TryParse(trimmedAmount, out var parsedAmount)
            || parsedAmount <= 0)
        {
            return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("02",
                "Invalid amount - Trường chỉ nhập số, tối đa 13 kí tự")));
        }

        val = parsedAmount;
    }

    var cleanTemplate = string.IsNullOrWhiteSpace(req.Template) ? "compact" : req.Template.Trim().ToLowerInvariant();

    if (cleanTemplate is not ("compact" or "qr_only" or "print"))
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("05",
            $"Template '{req.Template}' không hợp lệ. Chỉ hỗ trợ 3 mẫu: 'compact', 'qr_only' hoặc 'print'.")));
    }

    var cleanBin = req.BankBin.Trim();

    if (cleanBin.Length != 6 || !cleanBin.All(c => c >= '0' && c <= '9'))
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("03",
            "Invalid bankBin - Phải gồm đúng 6 chữ số")));
    }

    var cleanAccountNo = req.AccountNo.Trim();

    if (cleanAccountNo.Length > 25)
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("04",
            "Invalid accountNo - Tối đa 25 ký tự")));
    }

    var cleanDescription = req.Description.Trim();

    if (cleanDescription.Length > 95)
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("06",
            "Invalid description - Tối đa 95 ký tự")));
    }

    var bankInfo = await bankCatalog.FindAsync(cleanBin);

    if (bankInfo == null)
    {
        return (string.Empty, null!, string.Empty, BadRequest(ApiResponse<object>.Error("07",
            "Không tìm thấy thông tin ngân hàng")));
    }

    var resolvedServiceType = VietQrServiceType.Account;

    if (!string.IsNullOrWhiteSpace(req.ServiceType))
    {
        var st = req.ServiceType.Trim().ToLowerInvariant();

        if (st is "card" or "qribfttc" or "1")
            resolvedServiceType = VietQrServiceType.Card;
    }

    var cleanFormat = string.IsNullOrWhiteSpace(req.Format) ? "png" : req.Format.Trim().ToLowerInvariant();

    var imageFormat = cleanFormat switch
    {
        "webp" => QrImageFormat.Webp,
        "svg" => QrImageFormat.Svg,
        _ => QrImageFormat.Png
    };

    var merchantName = !string.IsNullOrWhiteSpace(req.AccountName)
        ? req.AccountName.Trim()
        : (!string.IsNullOrWhiteSpace(bankInfo.ShortName) ? bankInfo.ShortName : "VIETQR");

    // Tạo QR: chỉ thêm số tiền khi người dùng có truyền amount
    var qrBuilder = new VietQr()
        .SetBeneficiaryOrganization(cleanBin, cleanAccountNo, resolvedServiceType);

    if (val.HasValue)
    {
        qrBuilder.SetTransactionAmount(val.Value.ToString());
    }

    qrBuilder
        .SetMerchantInfo(merchantName, null)
        .SetAdditionalData(purpose: cleanDescription, billNumber: req.BillNumber);

    var finalQrContent = qrBuilder.Build();

    var renderOptions = new QrRenderOptions
    {
        Template = cleanTemplate,
        Format = imageFormat,
        Quality = 80,
        Size = req.Size is >= 100 and <= 2000 ? req.Size.Value : 500,
        CustomCenterLogoBase64 = req.CustomLogoBase64,
        CenterLogoRatio = req.CenterLogoRatio is >= 0.05f and <= 0.35f ? req.CenterLogoRatio.Value : 0.20f,
        BankBin = cleanBin,
        AccountNo = cleanAccountNo,
        AccountName = req.AccountName?.Trim(),
        Amount = val,
        Description = cleanDescription,
        BankFullName = bankInfo.Name,
        ServiceType = resolvedServiceType
    };

    return (finalQrContent, renderOptions, cleanFormat, null);
}
    #endregion
}
