namespace XRCultureTestServicesTool
{
    partial class TestServicesForm
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
            _textBoxLog = new TextBox();
            _buttonClose = new Button();
            _buttonViewModel = new Button();
            _buttonViewModelXML = new Button();
            _buttonGetViewers = new Button();
            _button3DReconstructionOpenMVG_MVS = new Button();
            _buttonGetConvertors = new Button();
            _buttonConvertModel = new Button();
            _button3DReconstructionNeRFStudio = new Button();
            groupBox1 = new GroupBox();
            _buttonGetMeshFilters = new Button();
            _buttonMeshLabSubdivide = new Button();
            _buttonMeshLabDecimate = new Button();
            groupBox2 = new GroupBox();
            _buttonGetPhotogrammetryServices = new Button();
            groupBox5 = new GroupBox();
            groupBox7 = new GroupBox();
            groupBox3 = new GroupBox();
            _buttonGenerateThumbnail = new Button();
            _buttonGetThumbnailGenerators = new Button();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            groupBox5.SuspendLayout();
            groupBox7.SuspendLayout();
            groupBox3.SuspendLayout();
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
            label1.Size = new Size(72, 15);
            label1.TabIndex = 1;
            label1.Text = "Hub:";
            // 
            // _textBoxLog
            // 
            _textBoxLog.Location = new Point(11, 42);
            _textBoxLog.Multiline = true;
            _textBoxLog.Name = "_textBoxLog";
            _textBoxLog.ReadOnly = true;
            _textBoxLog.ScrollBars = ScrollBars.Both;
            _textBoxLog.Size = new Size(633, 555);
            _textBoxLog.TabIndex = 4;
            // 
            // _buttonClose
            // 
            _buttonClose.Location = new Point(664, 574);
            _buttonClose.Name = "_buttonClose";
            _buttonClose.Size = new Size(134, 23);
            _buttonClose.TabIndex = 5;
            _buttonClose.Text = "Close";
            _buttonClose.UseVisualStyleBackColor = true;
            _buttonClose.Click += _buttonClose_Click;
            // 
            // _buttonViewModel
            // 
            _buttonViewModel.Location = new Point(7, 46);
            _buttonViewModel.Name = "_buttonViewModel";
            _buttonViewModel.Size = new Size(134, 23);
            _buttonViewModel.TabIndex = 6;
            _buttonViewModel.Text = "View Model";
            _buttonViewModel.UseVisualStyleBackColor = true;
            _buttonViewModel.Click += _buttonViewModel_Click;
            // 
            // _buttonViewModelXML
            // 
            _buttonViewModelXML.Location = new Point(7, 73);
            _buttonViewModelXML.Name = "_buttonViewModelXML";
            _buttonViewModelXML.Size = new Size(134, 23);
            _buttonViewModelXML.TabIndex = 7;
            _buttonViewModelXML.Text = "View Model (XML)";
            _buttonViewModelXML.UseVisualStyleBackColor = true;
            _buttonViewModelXML.Click += _buttonViewModelXML_Click;
            // 
            // _buttonGetViewers
            // 
            _buttonGetViewers.Location = new Point(7, 19);
            _buttonGetViewers.Name = "_buttonGetViewers";
            _buttonGetViewers.Size = new Size(134, 23);
            _buttonGetViewers.TabIndex = 8;
            _buttonGetViewers.Text = "Get Viewers";
            _buttonGetViewers.UseVisualStyleBackColor = true;
            _buttonGetViewers.Click += _buttonGetViewers_Click;
            // 
            // _button3DReconstructionOpenMVG_MVS
            // 
            _button3DReconstructionOpenMVG_MVS.Location = new Point(7, 48);
            _button3DReconstructionOpenMVG_MVS.Name = "_button3DReconstructionOpenMVG_MVS";
            _button3DReconstructionOpenMVG_MVS.Size = new Size(134, 23);
            _button3DReconstructionOpenMVG_MVS.TabIndex = 9;
            _button3DReconstructionOpenMVG_MVS.Text = "OpenMVG-OpenMVS";
            _button3DReconstructionOpenMVG_MVS.UseVisualStyleBackColor = true;
            _button3DReconstructionOpenMVG_MVS.Click += _button3DReconstructionOpenMVG_MVS_Click;
            // 
            // _buttonGetConvertors
            // 
            _buttonGetConvertors.Location = new Point(6, 22);
            _buttonGetConvertors.Name = "_buttonGetConvertors";
            _buttonGetConvertors.Size = new Size(134, 23);
            _buttonGetConvertors.TabIndex = 10;
            _buttonGetConvertors.Text = "Get Services";
            _buttonGetConvertors.UseVisualStyleBackColor = true;
            _buttonGetConvertors.Click += _buttonGetConvertors_Click;
            // 
            // _buttonConvertModel
            // 
            _buttonConvertModel.Location = new Point(6, 49);
            _buttonConvertModel.Name = "_buttonConvertModel";
            _buttonConvertModel.Size = new Size(134, 23);
            _buttonConvertModel.TabIndex = 11;
            _buttonConvertModel.Text = "Convert Model";
            _buttonConvertModel.UseVisualStyleBackColor = true;
            _buttonConvertModel.Click += _buttonConvertModel_Click;
            // 
            // _button3DReconstructionNeRFStudio
            // 
            _button3DReconstructionNeRFStudio.Location = new Point(7, 75);
            _button3DReconstructionNeRFStudio.Name = "_button3DReconstructionNeRFStudio";
            _button3DReconstructionNeRFStudio.Size = new Size(134, 23);
            _button3DReconstructionNeRFStudio.TabIndex = 12;
            _button3DReconstructionNeRFStudio.Text = "NeRFStudio";
            _button3DReconstructionNeRFStudio.UseVisualStyleBackColor = true;
            _button3DReconstructionNeRFStudio.Click += _button3DReconstructionNeRFStudio_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(_buttonGetMeshFilters);
            groupBox1.Controls.Add(_buttonMeshLabSubdivide);
            groupBox1.Controls.Add(_buttonMeshLabDecimate);
            groupBox1.Location = new Point(657, 322);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(147, 106);
            groupBox1.TabIndex = 16;
            groupBox1.TabStop = false;
            groupBox1.Text = "Mesh Filters";
            // 
            // _buttonGetMeshFilters
            // 
            _buttonGetMeshFilters.Location = new Point(7, 21);
            _buttonGetMeshFilters.Name = "_buttonGetMeshFilters";
            _buttonGetMeshFilters.Size = new Size(134, 23);
            _buttonGetMeshFilters.TabIndex = 12;
            _buttonGetMeshFilters.Text = "Get Services";
            _buttonGetMeshFilters.UseVisualStyleBackColor = true;
            _buttonGetMeshFilters.Click += _buttonGetMeshFilters_Click;
            // 
            // _buttonMeshLabSubdivide
            // 
            _buttonMeshLabSubdivide.Location = new Point(7, 75);
            _buttonMeshLabSubdivide.Name = "_buttonMeshLabSubdivide";
            _buttonMeshLabSubdivide.Size = new Size(134, 23);
            _buttonMeshLabSubdivide.TabIndex = 18;
            _buttonMeshLabSubdivide.Text = "Subdivide";
            _buttonMeshLabSubdivide.UseVisualStyleBackColor = true;
            _buttonMeshLabSubdivide.Click += _buttonMeshLabSubdivide_Click;
            // 
            // _buttonMeshLabDecimate
            // 
            _buttonMeshLabDecimate.Location = new Point(7, 48);
            _buttonMeshLabDecimate.Name = "_buttonMeshLabDecimate";
            _buttonMeshLabDecimate.Size = new Size(134, 23);
            _buttonMeshLabDecimate.TabIndex = 17;
            _buttonMeshLabDecimate.Text = "Decimate";
            _buttonMeshLabDecimate.UseVisualStyleBackColor = true;
            _buttonMeshLabDecimate.Click += _buttonMeshLabDecimate_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(_buttonGetPhotogrammetryServices);
            groupBox2.Controls.Add(_button3DReconstructionOpenMVG_MVS);
            groupBox2.Controls.Add(_button3DReconstructionNeRFStudio);
            groupBox2.Location = new Point(657, 434);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(147, 106);
            groupBox2.TabIndex = 17;
            groupBox2.TabStop = false;
            groupBox2.Text = "Photogrammetry";
            // 
            // _buttonGetPhotogrammetryServices
            // 
            _buttonGetPhotogrammetryServices.Location = new Point(7, 20);
            _buttonGetPhotogrammetryServices.Name = "_buttonGetPhotogrammetryServices";
            _buttonGetPhotogrammetryServices.Size = new Size(134, 23);
            _buttonGetPhotogrammetryServices.TabIndex = 20;
            _buttonGetPhotogrammetryServices.Text = "Get Services";
            _buttonGetPhotogrammetryServices.UseVisualStyleBackColor = true;
            _buttonGetPhotogrammetryServices.Click += _buttonGetPhotogrammetryServices_Click;
            // 
            // groupBox5
            // 
            groupBox5.Controls.Add(_buttonGetViewers);
            groupBox5.Controls.Add(_buttonViewModel);
            groupBox5.Controls.Add(_buttonViewModelXML);
            groupBox5.Location = new Point(657, 42);
            groupBox5.Name = "groupBox5";
            groupBox5.Size = new Size(147, 104);
            groupBox5.TabIndex = 18;
            groupBox5.TabStop = false;
            groupBox5.Text = "Viewers";
            // 
            // groupBox7
            // 
            groupBox7.Controls.Add(_buttonGetConvertors);
            groupBox7.Controls.Add(_buttonConvertModel);
            groupBox7.Location = new Point(657, 236);
            groupBox7.Name = "groupBox7";
            groupBox7.Size = new Size(147, 81);
            groupBox7.TabIndex = 18;
            groupBox7.TabStop = false;
            groupBox7.Text = "Convertors";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(_buttonGenerateThumbnail);
            groupBox3.Controls.Add(_buttonGetThumbnailGenerators);
            groupBox3.Location = new Point(657, 152);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(147, 78);
            groupBox3.TabIndex = 19;
            groupBox3.TabStop = false;
            groupBox3.Text = "Thumbnail Generator";
            // 
            // _buttonGenerateThumbnail
            // 
            _buttonGenerateThumbnail.Location = new Point(6, 46);
            _buttonGenerateThumbnail.Name = "_buttonGenerateThumbnail";
            _buttonGenerateThumbnail.Size = new Size(134, 23);
            _buttonGenerateThumbnail.TabIndex = 18;
            _buttonGenerateThumbnail.Text = "Generate Thumbnail";
            _buttonGenerateThumbnail.UseVisualStyleBackColor = true;
            _buttonGenerateThumbnail.Click += _buttonGenerateThumbnail_Click;
            // 
            // _buttonGetThumbnailGenerators
            // 
            _buttonGetThumbnailGenerators.Location = new Point(6, 19);
            _buttonGetThumbnailGenerators.Name = "_buttonGetThumbnailGenerators";
            _buttonGetThumbnailGenerators.Size = new Size(134, 23);
            _buttonGetThumbnailGenerators.TabIndex = 17;
            _buttonGetThumbnailGenerators.Text = "Get Services";
            _buttonGetThumbnailGenerators.UseVisualStyleBackColor = true;
            _buttonGetThumbnailGenerators.Click += _buttonGetThumbnailGenerators_Click;
            // 
            // TestServicesForm
            // 
            AcceptButton = _buttonClose;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(815, 609);
            ControlBox = false;
            Controls.Add(groupBox3);
            Controls.Add(groupBox7);
            Controls.Add(groupBox5);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(_textBoxLog);
            Controls.Add(_buttonClose);
            Controls.Add(label1);
            Controls.Add(_textBoxHubURL);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Name = "TestServicesForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "XRCulture Test Services Tool";
            FormClosed += TestServicesForm_FormClosed;
            Load += TestServicesForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox5.ResumeLayout(false);
            groupBox7.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox _textBoxHubURL;
        private Label label1;
        private TextBox _textBoxLog;
        private Button _buttonClose;
        private Button _buttonViewModel;
        private Button _buttonViewModelXML;
        private Button _buttonGetViewers;
        private Button _button3DReconstructionOpenMVG_MVS;
        private Button _buttonGetConvertors;
        private Button _buttonConvertModel;
        private Button _button3DReconstructionNeRFStudio;
        private GroupBox groupBox1;
        private Button _buttonMeshLabDecimate;
        private Button _buttonMeshLabSubdivide;
        private GroupBox groupBox2;
        private GroupBox groupBox5;
        private GroupBox groupBox7;
        private GroupBox groupBox3;
        private Button _buttonGenerateThumbnail;
        private Button _buttonGetThumbnailGenerators;
        private Button _buttonGetMeshFilters;
        private Button _buttonGetPhotogrammetryServices;
    }
}
