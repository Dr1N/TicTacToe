using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TicTacClient.Properties;
using WcfService;

namespace TicTacClient
{
    public partial class TicTac : Form
    {
        //Коммуникации

        private TicTacService ticTacClient;
        private ChannelFactory<TicTacService> cFactory;
        private Thread getGameThr;

        //Состояние

        private string login;
        private bool InGame;
        private bool IsTurn;
        private GameState.FIELD_STATE character; 

        //Контролы

        private PictureBox[] pbBoxes;

        public TicTac()
        {
            InitializeComponent();

            this.pbBoxes = new PictureBox[9];

            for (int i = 0; i < this.pbBoxes.Length; i++)
            {
                this.pbBoxes[i] = new PictureBox();
                this.pbBoxes[i].Click += new EventHandler(this.pictureBox_Click);
                this.pbBoxes[i].Name = "pictureBox" + i;
                this.pbBoxes[i].BorderStyle = BorderStyle.FixedSingle;
                this.pbBoxes[i].SizeMode = PictureBoxSizeMode.StretchImage;
                this.pbBoxes[i].Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                this.tableLayoutPanel1.Controls.Add(this.pbBoxes[i]);
            }
        }

        #region MenuEvents
        
        private void mnMainGame_DropDownOpening(object sender, EventArgs e)
        {
            this.mnGame.Enabled = !this.InGame;
        }

        private void mnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void mnGame_Click(object sender, EventArgs e)
        {
            if (this.InGame) { return; }    //На всякий случай :)

            try
            {
                frmLogin loginForm = new frmLogin();
                if (loginForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    this.cFactory = new ChannelFactory<TicTacService>("ENDPOINT");
                    this.ticTacClient = cFactory.CreateChannel();
                   
                    if (this.ticTacClient.Login(loginForm.UserLogin) == true)
                    {
                        this.InGame = true;
                        this.login = loginForm.UserLogin;
                        this.Text = "TicTac - " + this.login;
                        GameState gameState = this.ticTacClient.GetState(this.login);
                        this.character = (gameState.firstPlayer == this.login) ? GameState.FIELD_STATE.CROSS : GameState.FIELD_STATE.ZERO;
                        this.getGameThr = new Thread(GetGame);
                        this.getGameThr.IsBackground = true;
                        this.getGameThr.Start();
                    }
                    else
                    {
                        string message = "Не удалось присоединиться к игре. Возможно уже существует такой логин. Возможно другие проблемы с сервером. Попробуйте позже";
                        MessageBox.Show(message, "TicTac", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                Console.WriteLine(exc.StackTrace);
                MessageBox.Show(exc.Message, "Соединение с сервером", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region ControlEvents
        
        private void pictureBox_Click(object sender, EventArgs e)
        {
            try
            {
                //Проверка возможности хода

                if (!this.IsTurn) { return; }

                PictureBox pb = sender as PictureBox;
                if (pb == null) { return; }

                if (pb.Image != null) { return; }

                //Координата

                int pbIndex = Array.IndexOf(this.pbBoxes, pb);
                int pbY = pbIndex / (int)Math.Sqrt(this.pbBoxes.Length);
                int pbX = pbIndex - pbY * (int)Math.Sqrt(this.pbBoxes.Length);
                Point coord = new Point(pbX, pbY);

                //Ход

                Console.WriteLine("Ход:{0}\t", coord);

                lock (this)
                {
                    this.ticTacClient.Action(this.login, coord);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                MessageBox.Show("Не удалось сделать ход", "TicTac", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        #endregion

        private void GetGame()
        {
            try
            {
                Console.WriteLine("Поток обновления игры запущен");
                while (this.InGame)
                {
                    Thread.Sleep(500);
                    this.SetGameState();
                }
                Console.WriteLine("Поток обновления игры завершён(while)");
            }
            catch (Exception e)
            {
                this.InGame = false;
                this.IsTurn = false;
                Console.WriteLine("Поток обновления игры завершён(Exception)");
                Console.WriteLine(e.Message, "Получение данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetGameState()
        {
            try
            {
                GameState gs;
                lock (this)
                {
                    gs = this.ticTacClient.GetState(this.login);
                }

                //Игровое поле

                Image img;
                for (int i = 0; i < gs.field.Length; i++)
                {
                    if (gs.field[i] == GameState.FIELD_STATE.FREE)
                    {
                        this.Invoke(new Action(() => this.pbBoxes[i].Image = null));
                    }
                    else
                    {
                        img = (gs.field[i] == GameState.FIELD_STATE.CROSS) ? Resources.cross : Resources.zero;
                        this.Invoke(new Action(() => this.pbBoxes[i].Image = img));
                    }
                }

                //Окончание игры

                if (gs.state == GameState.GAME_STATE.END_GAME)
                {
                    Console.WriteLine("GAME OVER");
                    this.ticTacClient.ExitPlayer(this.login);
                    this.InGame = false;
                    this.IsTurn = false;
                    this.ticTacClient = null;
                    this.cFactory = null;
                    this.Invoke(new Action(() => this.sbStatus.Text = "Игра завершена"));
                    this.Invoke(new Action(() => this.Text = "TicTac"));
                    string message;
                    if (gs.currentPlayer == null)
                    {
                        message = "Ничья!";
                    }
                    else
                    {
                        message = (gs.currentPlayer == this.login) ? "Вы выйграли :)" : "Вы проиграли :(";
                    }
                    MessageBox.Show(message, "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                //Текущее состояние

                if (gs.state == GameState.GAME_STATE.WAIT_SECOND_PLAYER)
                {
                    this.Invoke(new Action(() => this.sbStatus.Text = "Ожидаем соперника..."));
                    return;
                }

                if (gs.currentPlayer == this.login)
                {
                    this.Invoke(new Action(() => this.sbStatus.Text = "Ваш ход"));
                    this.IsTurn = true;
                }
                else
                {
                    this.Invoke(new Action(() => this.sbStatus.Text = "Ожидаем ход соперника..."));
                    this.IsTurn = false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}