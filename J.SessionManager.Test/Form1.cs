using System;
using System.Windows.Forms;

namespace J.SessionManager.Test
{
    public partial class Form1 : Form
    {
        JSession _jSession;
        Form2 _form2;

        public Form1()
        {
            try
            {
                InitializeComponent();
                this._form2 = new Form2();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                _jSession = new JSession();
                _jSession.SetStringOutput((type, output) =>
                {
                    if (null != output)
                    {
                        this.textBox2.Text += output.Replace("\n", "\r\n");
                        this.textBox2.SelectionStart = this.textBox2.Text.Length;
                        this.textBox2.ScrollToCaret();
                    }
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
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _jSession.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}