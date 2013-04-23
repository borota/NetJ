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
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            this.Visible = false;    
        }

        private void Form2_Shown(object sender, EventArgs e)
        {
            this.textBox1.Text = string.Empty;
        }
    }
}
