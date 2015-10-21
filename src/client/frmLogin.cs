using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TicTacClient
{
    public partial class frmLogin : Form
    {
        public string UserLogin { get; set; }

        public frmLogin()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                if (this.tbLogin.Text.Trim().Length < 3)
                {
                    MessageBox.Show("Некорректный логин", "TicTac", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
                else
                {
                    this.UserLogin = this.tbLogin.Text.Trim();
                }
            }
        }
    }
}
