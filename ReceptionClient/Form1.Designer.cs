namespace ReceptionClient
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TableLayoutPanel _layoutPanel;
        private System.Windows.Forms.Label _lblName;
        private System.Windows.Forms.TextBox _txtName;
        private System.Windows.Forms.Label _lblBirthDate;
        private System.Windows.Forms.DateTimePicker _dtpBirthDate;
        private System.Windows.Forms.Label _lblGender;
        private System.Windows.Forms.ComboBox _cbGender;
        private System.Windows.Forms.Label _lblNationalId;
        private System.Windows.Forms.TextBox _txtNationalId;
        private System.Windows.Forms.Label _lblPhone;
        private System.Windows.Forms.TextBox _txtPhone;
        private System.Windows.Forms.Label _lblAddress;
        private System.Windows.Forms.TextBox _txtAddress;
        private System.Windows.Forms.Label _lblInsuranceType;
        private System.Windows.Forms.ComboBox _cbInsuranceType;
        private System.Windows.Forms.Label _lblInsuranceNumber;
        private System.Windows.Forms.TextBox _txtInsuranceNumber;
        private System.Windows.Forms.Button _btnRegister;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            _layoutPanel = new TableLayoutPanel();
            _lblName = new Label();
            _txtName = new TextBox();
            _lblBirthDate = new Label();
            _dtpBirthDate = new DateTimePicker();
            _lblGender = new Label();
            _cbGender = new ComboBox();
            _lblNationalId = new Label();
            _txtNationalId = new TextBox();
            _lblPhone = new Label();
            _txtPhone = new TextBox();
            _lblAddress = new Label();
            _txtAddress = new TextBox();
            _lblInsuranceType = new Label();
            _cbInsuranceType = new ComboBox();
            _lblInsuranceNumber = new Label();
            _txtInsuranceNumber = new TextBox();
            _btnRegister = new Button();
            _layoutPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _layoutPanel
            // 
            _layoutPanel.ColumnCount = 2;
            _layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            _layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            _layoutPanel.Controls.Add(_lblName, 0, 0);
            _layoutPanel.Controls.Add(_txtName, 1, 0);
            _layoutPanel.Controls.Add(_lblBirthDate, 0, 1);
            _layoutPanel.Controls.Add(_dtpBirthDate, 1, 1);
            _layoutPanel.Controls.Add(_lblGender, 0, 2);
            _layoutPanel.Controls.Add(_cbGender, 1, 2);
            _layoutPanel.Controls.Add(_lblNationalId, 0, 3);
            _layoutPanel.Controls.Add(_txtNationalId, 1, 3);
            _layoutPanel.Controls.Add(_lblPhone, 0, 4);
            _layoutPanel.Controls.Add(_txtPhone, 1, 4);
            _layoutPanel.Controls.Add(_lblAddress, 0, 5);
            _layoutPanel.Controls.Add(_txtAddress, 1, 5);
            _layoutPanel.Controls.Add(_lblInsuranceType, 0, 6);
            _layoutPanel.Controls.Add(_cbInsuranceType, 1, 6);
            _layoutPanel.Controls.Add(_lblInsuranceNumber, 0, 7);
            _layoutPanel.Controls.Add(_txtInsuranceNumber, 1, 7);
            _layoutPanel.Controls.Add(_btnRegister, 0, 8);
            _layoutPanel.Dock = DockStyle.Fill;
            _layoutPanel.Location = new Point(0, 0);
            _layoutPanel.Margin = new Padding(3, 2, 3, 2);
            _layoutPanel.Name = "_layoutPanel";
            _layoutPanel.Padding = new Padding(18, 15, 18, 15);
            _layoutPanel.RowCount = 9;
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            _layoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            _layoutPanel.Size = new Size(438, 412);
            _layoutPanel.TabIndex = 0;
            // 
            // _lblName
            // 
            _lblName.Dock = DockStyle.Fill;
            _lblName.Location = new Point(21, 15);
            _lblName.Name = "_lblName";
            _lblName.Size = new Size(134, 30);
            _lblName.TabIndex = 1;
            _lblName.Text = "Họ và tên (*):";
            _lblName.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _txtName
            // 
            _txtName.Dock = DockStyle.Fill;
            _txtName.Location = new Point(158, 19);
            _txtName.Margin = new Padding(0, 4, 0, 11);
            _txtName.MaxLength = 100;
            _txtName.Name = "_txtName";
            _txtName.Size = new Size(262, 23);
            _txtName.TabIndex = 2;
            // 
            // _lblBirthDate
            // 
            _lblBirthDate.Dock = DockStyle.Fill;
            _lblBirthDate.Location = new Point(21, 45);
            _lblBirthDate.Name = "_lblBirthDate";
            _lblBirthDate.Size = new Size(134, 30);
            _lblBirthDate.TabIndex = 3;
            _lblBirthDate.Text = "Ngày sinh (*):";
            _lblBirthDate.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _dtpBirthDate
            // 
            _dtpBirthDate.Dock = DockStyle.Fill;
            _dtpBirthDate.Format = DateTimePickerFormat.Short;
            _dtpBirthDate.Location = new Point(158, 49);
            _dtpBirthDate.Margin = new Padding(0, 4, 0, 11);
            _dtpBirthDate.MaxDate = new DateTime(2026, 7, 18, 0, 0, 0, 0);
            _dtpBirthDate.Name = "_dtpBirthDate";
            _dtpBirthDate.Size = new Size(262, 23);
            _dtpBirthDate.TabIndex = 4;
            _dtpBirthDate.Value = new DateTime(2026, 7, 18, 0, 0, 0, 0);
            // 
            // _lblGender
            // 
            _lblGender.Dock = DockStyle.Fill;
            _lblGender.Location = new Point(21, 75);
            _lblGender.Name = "_lblGender";
            _lblGender.Size = new Size(134, 30);
            _lblGender.TabIndex = 5;
            _lblGender.Text = "Giới tính (*):";
            _lblGender.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _cbGender
            // 
            _cbGender.Dock = DockStyle.Fill;
            _cbGender.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbGender.FormattingEnabled = true;
            _cbGender.Items.AddRange(new object[] { "Male", "Female", "Other" });
            _cbGender.Location = new Point(158, 79);
            _cbGender.Margin = new Padding(0, 4, 0, 11);
            _cbGender.Name = "_cbGender";
            _cbGender.Size = new Size(262, 23);
            _cbGender.TabIndex = 6;
            // 
            // _lblNationalId
            // 
            _lblNationalId.Dock = DockStyle.Fill;
            _lblNationalId.Location = new Point(21, 105);
            _lblNationalId.Name = "_lblNationalId";
            _lblNationalId.Size = new Size(134, 30);
            _lblNationalId.TabIndex = 7;
            _lblNationalId.Text = "Số CMND/CCCD:";
            _lblNationalId.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _txtNationalId
            // 
            _txtNationalId.Dock = DockStyle.Fill;
            _txtNationalId.Location = new Point(158, 109);
            _txtNationalId.Margin = new Padding(0, 4, 0, 11);
            _txtNationalId.MaxLength = 20;
            _txtNationalId.Name = "_txtNationalId";
            _txtNationalId.Size = new Size(262, 23);
            _txtNationalId.TabIndex = 8;
            // 
            // _lblPhone
            // 
            _lblPhone.Dock = DockStyle.Fill;
            _lblPhone.Location = new Point(21, 135);
            _lblPhone.Name = "_lblPhone";
            _lblPhone.Size = new Size(134, 30);
            _lblPhone.TabIndex = 9;
            _lblPhone.Text = "Số điện thoại:";
            _lblPhone.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _txtPhone
            // 
            _txtPhone.Dock = DockStyle.Fill;
            _txtPhone.Location = new Point(158, 139);
            _txtPhone.Margin = new Padding(0, 4, 0, 11);
            _txtPhone.MaxLength = 15;
            _txtPhone.Name = "_txtPhone";
            _txtPhone.Size = new Size(262, 23);
            _txtPhone.TabIndex = 10;
            // 
            // _lblAddress
            // 
            _lblAddress.Dock = DockStyle.Fill;
            _lblAddress.Location = new Point(21, 165);
            _lblAddress.Name = "_lblAddress";
            _lblAddress.Size = new Size(134, 30);
            _lblAddress.TabIndex = 11;
            _lblAddress.Text = "Địa chỉ thường trú:";
            _lblAddress.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _txtAddress
            // 
            _txtAddress.Dock = DockStyle.Fill;
            _txtAddress.Location = new Point(158, 169);
            _txtAddress.Margin = new Padding(0, 4, 0, 11);
            _txtAddress.MaxLength = 300;
            _txtAddress.Name = "_txtAddress";
            _txtAddress.Size = new Size(262, 23);
            _txtAddress.TabIndex = 12;
            // 
            // _lblInsuranceType
            // 
            _lblInsuranceType.Dock = DockStyle.Fill;
            _lblInsuranceType.Location = new Point(21, 195);
            _lblInsuranceType.Name = "_lblInsuranceType";
            _lblInsuranceType.Size = new Size(134, 30);
            _lblInsuranceType.TabIndex = 13;
            _lblInsuranceType.Text = "Loại bảo hiểm:";
            _lblInsuranceType.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _cbInsuranceType
            // 
            _cbInsuranceType.Dock = DockStyle.Fill;
            _cbInsuranceType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cbInsuranceType.FormattingEnabled = true;
            _cbInsuranceType.Items.AddRange(new object[] { "None", "Health Insurance", "Life Insurance", "Private" });
            _cbInsuranceType.Location = new Point(158, 199);
            _cbInsuranceType.Margin = new Padding(0, 4, 0, 11);
            _cbInsuranceType.Name = "_cbInsuranceType";
            _cbInsuranceType.Size = new Size(262, 23);
            _cbInsuranceType.TabIndex = 14;
            _cbInsuranceType.SelectedIndexChanged += cbInsuranceType_SelectedIndexChanged;
            // 
            // _lblInsuranceNumber
            // 
            _lblInsuranceNumber.Dock = DockStyle.Fill;
            _lblInsuranceNumber.Location = new Point(21, 225);
            _lblInsuranceNumber.Name = "_lblInsuranceNumber";
            _lblInsuranceNumber.Size = new Size(134, 30);
            _lblInsuranceNumber.TabIndex = 15;
            _lblInsuranceNumber.Text = "Số thẻ BHYT:";
            _lblInsuranceNumber.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _txtInsuranceNumber
            // 
            _txtInsuranceNumber.Dock = DockStyle.Fill;
            _txtInsuranceNumber.Enabled = false;
            _txtInsuranceNumber.Location = new Point(158, 229);
            _txtInsuranceNumber.Margin = new Padding(0, 4, 0, 11);
            _txtInsuranceNumber.MaxLength = 30;
            _txtInsuranceNumber.Name = "_txtInsuranceNumber";
            _txtInsuranceNumber.Size = new Size(262, 23);
            _txtInsuranceNumber.TabIndex = 16;
            // 
            // _btnRegister
            // 
            _btnRegister.Anchor = AnchorStyles.None;
            _btnRegister.BackColor = Color.DodgerBlue;
            _layoutPanel.SetColumnSpan(_btnRegister, 2);
            _btnRegister.Cursor = Cursors.Hand;
            _btnRegister.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnRegister.ForeColor = Color.White;
            _btnRegister.Location = new Point(153, 315);
            _btnRegister.Margin = new Padding(0, 8, 0, 0);
            _btnRegister.Name = "_btnRegister";
            _btnRegister.Size = new Size(131, 30);
            _btnRegister.TabIndex = 17;
            _btnRegister.Text = "Đăng Ký";
            _btnRegister.UseVisualStyleBackColor = false;
            _btnRegister.Click += btnRegister_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(438, 412);
            Controls.Add(_layoutPanel);
            Margin = new Padding(3, 2, 3, 2);
            MinimumSize = new Size(396, 422);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Mẫu Đăng Ký Bệnh Nhân";
            _layoutPanel.ResumeLayout(false);
            _layoutPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
    }
}
