namespace VietQR.Core;

public static class VietQrErrors
{
    public static readonly Error MissingParam = new("qr.missing_param", "Thiếu tham số bắt buộc (bankBin, accountNo).");
    public static readonly Error InvalidAmount = new("qr.invalid_amount", "Số tiền không hợp lệ (nhỏ hơn hoặc bằng 0).");
    public static readonly Error InvalidBin = new("qr.invalid_bin", "Mã BIN sai định dạng (bắt buộc đúng 6 chữ số).");
    public static readonly Error BankNotFound = new("qr.bank_not_found", "Không tìm thấy thông tin ngân hàng trong danh mục NAPAS.");
    public static readonly Error InvalidTemplate = new("qr.invalid_template", "Template không hợp lệ (chỉ chấp nhận 'compact', 'qr_only' hoặc 'print').");
    public static readonly Error SystemError = new("qr.system_error", "Lỗi xử lý hệ thống.");

    // Parser & Decoder Errors
    public static readonly Error InvalidPayload = new("qr.invalid_payload", "Chuỗi mã QR không đúng định dạng EMVCo TLV hoặc vượt quá độ dài cho phép.");
    public static readonly Error InvalidCrc = new("qr.invalid_crc", "Mã kiểm tra CRC16 (Tag 63) không khớp với nội dung dữ liệu mã QR.");
    public static readonly Error NotVietQr = new("qr.not_vietqr", "Mã QR không phải là mã thanh toán VietQR hợp lệ (thiếu Tag 38 hoặc AID A000000727).");
    public static readonly Error ImageDecodeFailed = new("qr.image_decode_failed", "Không tìm thấy hoặc không thể giải mã mã QR từ hình ảnh cung cấp.");
}
