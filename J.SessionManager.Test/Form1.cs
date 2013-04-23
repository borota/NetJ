using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace J.SessionManager.Test
{
    public partial class Form1 : Form
    {
        JSession _jSession;
        Form2 _form2;

        public Form1()
        {
            InitializeComponent();
            this._form2 = new Form2();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _jSession = new JSession();
            _jSession.SetStringOutput((type, output) =>
            {
                this.textBox2.Text +=  output.Replace("\n", "\r\n");
                this.textBox2.SelectionStart = this.textBox2.Text.Length;
                this.textBox2.ScrollToCaret();
            });
            _jSession.SetInput((p) =>
            {
                this._form2.ShowDialog();
                var result = this._form2.textBox1.Text;
                this.textBox2.Text += result + "\r\n";
                this.textBox2.SelectionStart = this.textBox2.Text.Length;
                this.textBox2.ScrollToCaret();
                return result;
            });
            _jSession.ApplyCallbacks();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _jSession.Dispose();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var sentence = this.textBox1.Text.Trim();
            if (string.Empty == sentence)
            {
                MessageBox.Show(this, "Please enter sentence to execute!");
            }
            else
            {
                this.textBox1.Text = string.Empty;
                this.textBox2.Text += "    " + sentence + "\r\n";
                this.textBox2.SelectionStart = this.textBox2.Text.Length;
                this.textBox2.ScrollToCaret();
                _jSession.Do(sentence);
            }
        }
    }
}
