using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Unitronics.ComDriver
{
    public partial class UserControl1 : UserControl
    {
        private String IP_Adress = "";
        private int Port_Id = 0;

        public UserControl1()
        {
            InitializeComponent();
        }

        private void UserControl1_Load(object sender, EventArgs e)
        {
        }

        private void login_clicked(object sender, EventArgs e)
        {
        }

        private void IP_clicked(object sender, EventArgs e)
        {
        }

        private void Port_Click(object sender, EventArgs e)
        {
        }

        //IP Adress field
        private void IP_Changed(object sender, EventArgs e)
        {
            TextBox t = (TextBox) sender;
            IP_Adress += t.Text;
        }

        //Port field
        private void Port_Changed(object sender, EventArgs e)
        {
            TextBox t = (TextBox) sender;
            Port_Id = Int32.Parse(t.Text);
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
        }
    }
}