namespace XRCultureRegisterTool
{
    partial class RegisterViewerForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _textBoxHubURL = new TextBox();
            label1 = new Label();
            _buttonRegister = new Button();
            _buttonAuthorize = new Button();
            _textBoxLog = new TextBox();
            _buttonClose = new Button();
            _buttonGetViewers = new Button();
            _buttonGetConvertors = new Button();
            _buttonGetThumbnailGenerators = new Button();
            _buttonGetMeshFilters = new Button();
            _buttonGetPhotogrammetryServices = new Button();
            _buttonGetRepositories = new Button();
            SuspendLayout();
            // 
            // _textBoxHubURL
            // 
            _textBoxHubURL.Location = new Point(87, 13);
            _textBoxHubURL.Name = "_textBoxHubURL";
            _textBoxHubURL.Size = new Size(255, 23);
            _textBoxHubURL.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(10, 17);
            label1.Name = "label1";
            label1.Size = new Size(33, 15);
            label1.TabIndex = 1;
            label1.Text = "Hub:";
            // 
            // _buttonRegister
            // 
            _buttonRegister.Enabled = false;
            _buttonRegister.Location = new Point(483, 13);
            _buttonRegister.Name = "_buttonRegister";
            _buttonRegister.Size = new Size(120, 23);
            _buttonRegister.TabIndex = 2;
            _buttonRegister.Text = "Register";
            _buttonRegister.UseVisualStyleBackColor = true;
            _buttonRegister.Click += _buttonRegister_Click;
            // 
            // _buttonAuthorize
            // 
            _buttonAuthorize.Location = new Point(348, 13);
            _buttonAuthorize.Name = "_buttonAuthorize";
            _buttonAuthorize.Size = new Size(128, 23);
            _buttonAuthorize.TabIndex = 3;
            _buttonAuthorize.Text = "Authorize";
            _buttonAuthorize.UseVisualStyleBackColor = true;
            _buttonAuthorize.Click += _buttonAuthorize_Click;
            // 
            // _textBoxLog
            // 
            _textBoxLog.Location = new Point(11, 42);
            _textBoxLog.Multiline = true;
            _textBoxLog.Name = "_textBoxLog";
            _textBoxLog.ReadOnly = true;
            _textBoxLog.ScrollBars = ScrollBars.Both;
            _textBoxLog.Size = new Size(592, 367);
            _textBoxLog.TabIndex = 4;
            // 
            // _buttonClose
            // 
            _buttonClose.Location = new Point(609, 386);
            _buttonClose.Name = "_buttonClose";
            _buttonClose.Size = new Size(168, 23);
            _buttonClose.TabIndex = 5;
            _buttonClose.Text = "Close";
            _buttonClose.UseVisualStyleBackColor = true;
            _buttonClose.Click += _buttonClose_Click;
            // 
            // _buttonGetViewers
            // 
            _buttonGetViewers.Location = new Point(609, 42);
            _buttonGetViewers.Name = "_buttonGetViewers";
            _buttonGetViewers.Size = new Size(168, 23);
            _buttonGetViewers.TabIndex = 8;
            _buttonGetViewers.Text = "Viewer";
            _buttonGetViewers.UseVisualStyleBackColor = true;
            _buttonGetViewers.Click += _buttonGetViewers_Click;
            // 
            // _buttonGetConvertors
            // 
            _buttonGetConvertors.Location = new Point(609, 100);
            _buttonGetConvertors.Name = "_buttonGetConvertors";
            _buttonGetConvertors.Size = new Size(168, 23);
            _buttonGetConvertors.TabIndex = 9;
            _buttonGetConvertors.Text = "Convertor";
            _buttonGetConvertors.UseVisualStyleBackColor = true;
            _buttonGetConvertors.Click += _buttonGetConvertors_Click;
            // 
            // _buttonGetThumbnailGenerators
            // 
            _buttonGetThumbnailGenerators.Location = new Point(609, 71);
            _buttonGetThumbnailGenerators.Name = "_buttonGetThumbnailGenerators";
            _buttonGetThumbnailGenerators.Size = new Size(168, 23);
            _buttonGetThumbnailGenerators.TabIndex = 10;
            _buttonGetThumbnailGenerators.Text = "Thumbnail Generator";
            _buttonGetThumbnailGenerators.UseVisualStyleBackColor = true;
            _buttonGetThumbnailGenerators.Click += _buttonGetThumbnailGenerators_Click;
            // 
            // _buttonGetMeshFilters
            // 
            _buttonGetMeshFilters.Location = new Point(609, 129);
            _buttonGetMeshFilters.Name = "_buttonGetMeshFilters";
            _buttonGetMeshFilters.Size = new Size(168, 23);
            _buttonGetMeshFilters.TabIndex = 11;
            _buttonGetMeshFilters.Text = "Mesh Filter";
            _buttonGetMeshFilters.UseVisualStyleBackColor = true;
            _buttonGetMeshFilters.Click += _buttonGetMeshFilters_Click;
            // 
            // _buttonGetPhotogrammetryServices
            // 
            _buttonGetPhotogrammetryServices.Location = new Point(609, 158);
            _buttonGetPhotogrammetryServices.Name = "_buttonGetPhotogrammetryServices";
            _buttonGetPhotogrammetryServices.Size = new Size(168, 23);
            _buttonGetPhotogrammetryServices.TabIndex = 12;
            _buttonGetPhotogrammetryServices.Text = "Photogrammetry";
            _buttonGetPhotogrammetryServices.UseVisualStyleBackColor = true;
            _buttonGetPhotogrammetryServices.Click += _buttonGetPhotogrammetryServices_Click;
            // 
            // _buttonGetRepositories
            // 
            _buttonGetRepositories.Location = new Point(609, 187);
            _buttonGetRepositories.Name = "_buttonGetRepositories";
            _buttonGetRepositories.Size = new Size(168, 23);
            _buttonGetRepositories.TabIndex = 13;
            _buttonGetRepositories.Text = "Repository";
            _buttonGetRepositories.UseVisualStyleBackColor = true;
            _buttonGetRepositories.Click += _buttonGetRepositories_Click;
            // 
            // RegisterViewerForm
            // 
            AcceptButton = _buttonClose;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(783, 417);
            ControlBox = false;
            Controls.Add(_buttonGetRepositories);
            Controls.Add(_buttonGetPhotogrammetryServices);
            Controls.Add(_buttonGetMeshFilters);
            Controls.Add(_buttonGetThumbnailGenerators);
            Controls.Add(_buttonGetConvertors);
            Controls.Add(_buttonGetViewers);
            Controls.Add(_buttonClose);
            Controls.Add(_buttonAuthorize);
            Controls.Add(_buttonRegister);
            Controls.Add(label1);
            Controls.Add(_textBoxHubURL);
            Controls.Add(_textBoxLog);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Name = "RegisterViewerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "XRCulture Register Tool";
            FormClosed += RegisterViewerForm_FormClosed;
            Load += RegisterViewerForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox _textBoxHubURL;
        private Label label1;
        private Button _buttonRegister;
        private Button _buttonAuthorize;
        private TextBox _textBoxLog;
        private Button _buttonClose;
        private Button _buttonGetViewers;
        private Button _buttonGetConvertors;
        private Button _buttonGetThumbnailGenerators;
        private Button _buttonGetMeshFilters;
        private Button _buttonGetPhotogrammetryServices;
        private Button _buttonGetRepositories;
    }
}
