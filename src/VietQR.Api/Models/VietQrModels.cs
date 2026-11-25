using System.Text.Json.Serialization;

namespace VietQR.Api.Models;

/// <summary>
/// Yêu cầu tạo mã thanh toán VietQR và render ảnh thẻ.
/// </summary>
public class VietQrGenerateRequest
{
    /// <summary>
    /// Mã BIN 6 chữ số của ngân hàng thụ hưởng (VD: "970422" cho MBBank, "970436" cho Vietcombank).
    /// </summary>
    /// <example>970422</example>
    [JsonPropertyName("bankBin")]
    public string BankBin { get; set; } = string.Empty;

    /// <summary>
    /// Số tài khoản, số thẻ hoặc tài khoản định danh/ảo của người thụ hưởng (tối đa 25 ký tự).
    /// </summary>
    /// <example>056789799999</example>
    [JsonPropertyName("accountNo")]
    public string AccountNo { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thanh toán (VNĐ). Số nguyên dương, tối đa 13 chữ số.
    /// </summary>
    /// <example>50000</example>
    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    /// <summary>
    /// Nội dung thanh toán / chuyển khoản (tối đa 95 ký tự theo chuẩn EMVCo Tag 62.08).
    /// </summary>
    /// <example>Thanh toan don hang</example>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Tên chủ tài khoản người thụ hưởng (tùy chọn, hiển thị khi dùng mẫu 'print').
    /// </summary>
    /// <example>VO NGOC HIEN</example>
    [JsonPropertyName("accountName")]
    public string? AccountName { get; set; }

    /// <summary>
    /// Mẫu thiết kế giao diện ảnh thẻ: 'compact' (mặc định), 'qr_only' hoặc 'print'.
    /// </summary>
    /// <example>compact</example>
    [JsonPropertyName("template")]
    public string? Template { get; set; } = "compact";

    /// <summary>
    /// Loại dịch vụ: 'account' (mặc định - QRIBFTTA chuyển qua tài khoản) hoặc 'card' (QRIBFTTC chuyển qua thẻ).
    /// </summary>
    /// <example>account</example>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; } = "account";

    /// <summary>
    /// Số hóa đơn / mã tham chiếu đơn hàng (tùy chọn, Tag 62.01 tối đa 25 ký tự).
    /// </summary>
    /// <example>HD12345</example>
    [JsonPropertyName("billNumber")]
    public string? BillNumber { get; set; }

    /// <summary>
    /// Định dạng xuất ảnh: 'png' (mặc định), 'webp' hoặc 'svg'.
    /// </summary>
    /// <example>png</example>
    [JsonPropertyName("format")]
    public string? Format { get; set; } = "png";

    /// <summary>
    /// Kích thước cạnh px của mẫu 'qr_only' (100-2000, mặc định 500).
    /// </summary>
    /// <example>500</example>
    [JsonPropertyName("size")]
    public int? Size { get; set; } = 500;

    /// <summary>
    /// Logo thương hiệu riêng đặt ở giữa mã QR (chuỗi Base64 hoặc Data URL, tùy chọn).
    /// </summary>
    [JsonPropertyName("customLogoBase64")]
    public string? CustomLogoBase64 { get; set; }

    /// <summary>
    /// Tỷ lệ kích thước logo thương hiệu so với QR (mặc định 0.20, khoảng an toàn 0.10 - 0.28).
    /// </summary>
    /// <example>0.20</example>
    [JsonPropertyName("centerLogoRatio")]
    public float? CenterLogoRatio { get; set; } = 0.20f;
}

/// <summary>
/// Yêu cầu giải mã QR từ chuỗi Base64 của ảnh.
/// </summary>
public class VietQrDecodeRequest
{
    /// <summary>
    /// Chuỗi Base64 hoặc Data URL của file ảnh chứa mã QR cần đọc.
    /// </summary>
    /// <example>data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...</example>
    [JsonPropertyName("imageBase64")]
    public string ImageBase64 { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu phân tích chuỗi văn bản mã EMVCo / VietQR.
/// </summary>
public class VietQrParseRequest
{
    /// <summary>
    /// Chuỗi văn bản thô theo chuẩn EMVCo (bắt đầu bằng 000201...).
    /// </summary>
    /// <example>00020101021238560010A0000007270126000697042201120567897999990208QRIBFTTA53037045405500005802VN62140810thanh toan63045083</example>
    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = string.Empty;
}
