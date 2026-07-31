namespace MadWizard.Desomnia.Minion
{
    partial class SleeplessConfigurationWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SleeplessConfigurationWindow));
            buttonOK = new System.Windows.Forms.Button();
            buttonCancel = new System.Windows.Forms.Button();
            checkBoxTime = new System.Windows.Forms.CheckBox();
            checkBoxPermanent = new System.Windows.Forms.CheckBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            labelDescription = new System.Windows.Forms.Label();
            dateTimePicker = new System.Windows.Forms.DateTimePicker();
            groupBoxUsage = new System.Windows.Forms.GroupBox();
            checkBoxUsage = new System.Windows.Forms.CheckBox();
            treeListViewTokens = new BrightIdeasSoftware.TreeListView();
            olvColumnName = new BrightIdeasSoftware.OLVColumn();
            olvColumnType = new BrightIdeasSoftware.OLVColumn();
            olvColumnDuration = new BrightIdeasSoftware.OLVColumn();
            progressBarInspection = new System.Windows.Forms.ProgressBar();
            timer = new System.Windows.Forms.Timer(components);
            toolTipProgress = new System.Windows.Forms.ToolTip(components);
            groupBox1.SuspendLayout();
            groupBoxUsage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)treeListViewTokens).BeginInit();
            SuspendLayout();
            // 
            // buttonOK
            // 
            buttonOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonOK.AutoSize = true;
            buttonOK.Location = new System.Drawing.Point(310, 952);
            buttonOK.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new System.Drawing.Size(173, 45);
            buttonOK.TabIndex = 4;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += buttonOK_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            buttonCancel.Location = new System.Drawing.Point(492, 952);
            buttonCancel.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(173, 45);
            buttonCancel.TabIndex = 5;
            buttonCancel.Text = "Abbrechen";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += buttonCancel_Click;
            // 
            // checkBoxTime
            // 
            checkBoxTime.AutoSize = true;
            checkBoxTime.Location = new System.Drawing.Point(39, 64);
            checkBoxTime.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            checkBoxTime.Name = "checkBoxTime";
            checkBoxTime.Size = new System.Drawing.Size(188, 36);
            checkBoxTime.TabIndex = 14;
            checkBoxTime.Text = "Zeitgesteuert";
            checkBoxTime.UseVisualStyleBackColor = true;
            checkBoxTime.CheckedChanged += checkBoxTime_CheckedChanged;
            // 
            // checkBoxPermanent
            // 
            checkBoxPermanent.AutoSize = true;
            checkBoxPermanent.Location = new System.Drawing.Point(39, 16);
            checkBoxPermanent.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            checkBoxPermanent.Name = "checkBoxPermanent";
            checkBoxPermanent.Size = new System.Drawing.Size(152, 36);
            checkBoxPermanent.TabIndex = 13;
            checkBoxPermanent.Text = "Dauerhaft";
            checkBoxPermanent.UseVisualStyleBackColor = true;
            checkBoxPermanent.CheckedChanged += checkBoxPermanent_CheckedChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBox1.Controls.Add(labelDescription);
            groupBox1.Controls.Add(dateTimePicker);
            groupBox1.Enabled = false;
            groupBox1.Location = new System.Drawing.Point(20, 66);
            groupBox1.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new System.Windows.Forms.Padding(6, 7, 6, 7);
            groupBox1.Size = new System.Drawing.Size(646, 202);
            groupBox1.TabIndex = 12;
            groupBox1.TabStop = false;
            // 
            // labelDescription
            // 
            labelDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            labelDescription.Location = new System.Drawing.Point(17, 111);
            labelDescription.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            labelDescription.Name = "labelDescription";
            labelDescription.Size = new System.Drawing.Size(585, 84);
            labelDescription.TabIndex = 3;
            labelDescription.Text = "Zur eingestellten Zeit wird der Schlaflos-Modus automatisch deaktiviert.\r\n";
            labelDescription.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // dateTimePicker
            // 
            dateTimePicker.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            dateTimePicker.Location = new System.Drawing.Point(13, 49);
            dateTimePicker.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            dateTimePicker.Name = "dateTimePicker";
            dateTimePicker.Size = new System.Drawing.Size(615, 39);
            dateTimePicker.TabIndex = 0;
            // 
            // groupBoxUsage
            // 
            groupBoxUsage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBoxUsage.Controls.Add(checkBoxUsage);
            groupBoxUsage.Controls.Add(treeListViewTokens);
            groupBoxUsage.Controls.Add(progressBarInspection);
            groupBoxUsage.Location = new System.Drawing.Point(19, 282);
            groupBoxUsage.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            groupBoxUsage.Name = "groupBoxUsage";
            groupBoxUsage.Padding = new System.Windows.Forms.Padding(6, 7, 6, 7);
            groupBoxUsage.Size = new System.Drawing.Size(646, 658);
            groupBoxUsage.TabIndex = 17;
            groupBoxUsage.TabStop = false;
            // 
            // checkBoxUsage
            // 
            checkBoxUsage.AutoSize = true;
            checkBoxUsage.Checked = true;
            checkBoxUsage.CheckState = System.Windows.Forms.CheckState.Checked;
            checkBoxUsage.Location = new System.Drawing.Point(20, -3);
            checkBoxUsage.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            checkBoxUsage.Name = "checkBoxUsage";
            checkBoxUsage.Size = new System.Drawing.Size(250, 36);
            checkBoxUsage.TabIndex = 18;
            checkBoxUsage.Text = "Nutzungsgesteuert";
            checkBoxUsage.UseVisualStyleBackColor = true;
            checkBoxUsage.CheckedChanged += checkBoxUsage_CheckedChanged;
            // 
            // treeListViewTokens
            // 
            treeListViewTokens.AllColumns.Add(olvColumnName);
            treeListViewTokens.AllColumns.Add(olvColumnType);
            treeListViewTokens.AllColumns.Add(olvColumnDuration);
            treeListViewTokens.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            treeListViewTokens.CellEditUseWholeCell = false;
            treeListViewTokens.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { olvColumnName, olvColumnType });
            treeListViewTokens.FullRowSelect = true;
            treeListViewTokens.Location = new System.Drawing.Point(13, 40);
            treeListViewTokens.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            treeListViewTokens.Name = "treeListViewTokens";
            treeListViewTokens.ShowFilterMenuOnRightClick = false;
            treeListViewTokens.ShowGroups = false;
            treeListViewTokens.Size = new System.Drawing.Size(621, 553);
            treeListViewTokens.TabIndex = 20;
            treeListViewTokens.UseCompatibleStateImageBehavior = false;
            treeListViewTokens.View = System.Windows.Forms.View.Details;
            treeListViewTokens.VirtualMode = true;
            // 
            // olvColumnName
            // 
            olvColumnName.Text = "Name";
            olvColumnName.Width = 280;
            // 
            // olvColumnType
            // 
            olvColumnType.Text = "Typ";
            olvColumnType.Width = 280;
            // 
            // olvColumnDuration
            // 
            olvColumnDuration.DisplayIndex = 2;
            olvColumnDuration.IsVisible = false;
            olvColumnDuration.Text = "Dauer";
            olvColumnDuration.Width = 120;
            // 
            // progressBarInspection
            // 
            progressBarInspection.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            progressBarInspection.Cursor = System.Windows.Forms.Cursors.AppStarting;
            progressBarInspection.ForeColor = System.Drawing.Color.Gold;
            progressBarInspection.Location = new System.Drawing.Point(13, 607);
            progressBarInspection.Margin = new System.Windows.Forms.Padding(6, 7, 6, 7);
            progressBarInspection.Name = "progressBarInspection";
            progressBarInspection.Size = new System.Drawing.Size(620, 34);
            progressBarInspection.Step = 1;
            progressBarInspection.TabIndex = 19;
            progressBarInspection.Value = 50;
            // 
            // timer
            // 
            timer.Enabled = true;
            timer.Tick += timer_Tick;
            // 
            // SleeplessConfigurationWindow
            // 
            AcceptButton = buttonOK;
            AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new System.Drawing.Size(685, 1011);
            Controls.Add(groupBoxUsage);
            Controls.Add(checkBoxTime);
            Controls.Add(buttonCancel);
            Controls.Add(checkBoxPermanent);
            Controls.Add(groupBox1);
            Controls.Add(buttonOK);
            HelpButton = true;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new System.Drawing.Size(533, 595);
            Name = "SleeplessConfigurationWindow";
            ShowInTaskbar = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Schlaflos konfigurieren";
            TopMost = true;
            groupBox1.ResumeLayout(false);
            groupBoxUsage.ResumeLayout(false);
            groupBoxUsage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)treeListViewTokens).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.CheckBox checkBoxTime;
        private System.Windows.Forms.CheckBox checkBoxPermanent;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.DateTimePicker dateTimePicker;
        private System.Windows.Forms.GroupBox groupBoxUsage;
        private System.Windows.Forms.CheckBox checkBoxUsage;
        private System.Windows.Forms.ProgressBar progressBarInspection;
        private BrightIdeasSoftware.TreeListView treeListViewTokens;
        private BrightIdeasSoftware.OLVColumn olvColumnName;
        private BrightIdeasSoftware.OLVColumn olvColumnType;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.ToolTip toolTipProgress;
        private BrightIdeasSoftware.OLVColumn olvColumnDuration;
    }
}