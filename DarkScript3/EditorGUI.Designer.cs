namespace DarkScript3
{
    partial class EditorGUI
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditorGUI));
            this.editor = new FastColoredTextBoxNS.FastColoredTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.editor)).BeginInit();
            this.SuspendLayout();
            // 
            // editor
            // 
            this.editor.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.editor.AutoIndentChars = false;
            this.editor.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.editor.AutoScrollMinSize = new System.Drawing.Size(23, 12);
            this.editor.BackBrush = null;
            this.editor.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.editor.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.editor.CaretColor = System.Drawing.Color.White;
            this.editor.CharHeight = 12;
            this.editor.CharWidth = 6;
            this.editor.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.editor.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.editor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editor.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.editor.ForeColor = System.Drawing.Color.Gainsboro;
            this.editor.IndentBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.editor.IsReplaceMode = false;
            this.editor.LeftBracket = '(';
            this.editor.LeftBracket2 = '{';
            this.editor.LineNumberColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.editor.Location = new System.Drawing.Point(0, 0);
            this.editor.Name = "editor";
            this.editor.Paddings = new System.Windows.Forms.Padding(0);
            this.editor.RightBracket = ')';
            this.editor.RightBracket2 = '}';
            this.editor.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.editor.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("editor.ServiceColors")));
            this.editor.Size = new System.Drawing.Size(150, 150);
            this.editor.TabIndex = 0;
            this.editor.ToolTipDelay = 100;
            this.editor.Zoom = 100;
            this.editor.ToolTipNeeded += new System.EventHandler<FastColoredTextBoxNS.ToolTipNeededEventArgs>(this.Editor_ToolTipNeeded);
            this.editor.TextChanged += new System.EventHandler<FastColoredTextBoxNS.TextChangedEventArgs>(this.Editor_TextChanged);
            this.editor.SelectionChanged += new System.EventHandler(this.Editor_SelectionChanged);
            this.editor.VisibleRangeChangedDelayed += new System.EventHandler(this.Editor_VisibleRangeChangedDelayed);
            this.editor.CustomAction += new System.EventHandler<FastColoredTextBoxNS.CustomActionEventArgs>(this.Editor_CustomAction);
            this.editor.Scroll += new System.Windows.Forms.ScrollEventHandler(this.Editor_Scroll);
            this.editor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Editor_KeyDown);
            this.editor.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Editor_MouseMove);
            this.editor.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Editor_MouseUp);
            // 
            // EditorGUI
            // 
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.editor);
            this.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.Name = "EditorGUI";
            ((System.ComponentModel.ISupportInitialize)(this.editor)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        public FastColoredTextBoxNS.FastColoredTextBox editor;
    }
}

