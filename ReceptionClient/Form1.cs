using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReceptionClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            
            // Thiết lập giá trị mặc định cho Dropdown (ComboBox) sau khi UI đã khởi tạo
            _cbGender.SelectedIndex = 0;
            _cbInsuranceType.SelectedIndex = 0;
            _dtpBirthDate.MaxDate = DateTime.Today;
        }

        private void cbInsuranceType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Tự động bật/tắt ô nhập Số thẻ BHYT
            _txtInsuranceNumber.Enabled = _cbInsuranceType.SelectedItem?.ToString() != "None";
            if (!_txtInsuranceNumber.Enabled) _txtInsuranceNumber.Clear();
        }

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            // --- 1. VALIDATION CHẶT CHẼ ---
            var fullName = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                MessageBox.Show("Vui lòng nhập Họ và tên!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            var phone = _txtPhone.Text.Trim();
            if (!string.IsNullOrEmpty(phone) && !Regex.IsMatch(phone, @"^[0-9]{9,15}$"))
            {
                MessageBox.Show("Số điện thoại không hợp lệ (Chỉ chứa số, độ dài 9-15)!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPhone.Focus();
                return;
            }

            // --- 2. PREPARE PAYLOAD ---
            var payload = new
            {
                FullName = fullName,
                DateOfBirth = _dtpBirthDate.Value.ToString("yyyy-MM-dd"), // Chuẩn ISO cho DateOnly
                Gender = _cbGender.SelectedItem?.ToString() ?? "Other",
                NationalId = string.IsNullOrEmpty(_txtNationalId.Text.Trim()) ? null : _txtNationalId.Text.Trim(),
                PhoneNumber = string.IsNullOrEmpty(phone) ? null : phone,
                Address = string.IsNullOrEmpty(_txtAddress.Text.Trim()) ? null : _txtAddress.Text.Trim(),
                InsuranceType = _cbInsuranceType.SelectedItem?.ToString() == "None" ? null : _cbInsuranceType.SelectedItem?.ToString(),
                InsuranceNumber = string.IsNullOrEmpty(_txtInsuranceNumber.Text.Trim()) ? null : _txtInsuranceNumber.Text.Trim()
            };

            // Khóa giao diện (Chống double-click)
            _btnRegister.Enabled = false;
            _btnRegister.Text = "Đang xử lý...";
            this.UseWaitCursor = true;

            try
            {
                // --- 3. SEND API REQUEST ---
                var response = await Program.ApiClient.PostAsJsonAsync("api/patients/register", payload);

                // --- 4. HANDLE RESPONSE ---
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Đăng ký bệnh nhân thành công! Hồ sơ đang được xử lý ngầm và sẽ sớm có mặt trên ClinicalAPI.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ResetForm();
                }
                else
                {
                    // Đọc nội dung lỗi chi tiết từ server
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Đăng ký thất bại. Lỗi HTTP {response.StatusCode}\nChi tiết: {errorMsg}", "Lỗi hệ thống", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Lỗi kết nối máy chủ ReceptionAPI (Port 5004).\nVui lòng kiểm tra lại backend.\n\nChi tiết: {ex.Message}", "Lỗi Mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Mở khóa giao diện (SynchronizationContext tự động return thread)
                _btnRegister.Enabled = true;
                _btnRegister.Text = "Đăng Ký";
                this.UseWaitCursor = false;
            }
        }

        private void ResetForm()
        {
            _txtName.Clear();
            _dtpBirthDate.Value = DateTime.Today;
            _cbGender.SelectedIndex = 0;
            _txtNationalId.Clear();
            _txtPhone.Clear();
            _txtAddress.Clear();
            _cbInsuranceType.SelectedIndex = 0;
            _txtName.Focus();
        }
    }
}
