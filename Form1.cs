using spacewar;
using WMPLib;


using spacewar;
using WMPLib;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;

namespace spacewar
{

    public partial class Form1 : Form
    {
        WindowsMediaPlayer gameMedia;
        WindowsMediaPlayer shootMedia;
        WindowsMediaPlayer boomMedia;

        PictureBox[] stars;
        PictureBox[] munitions;
        List<PictureBox> enemies = new();
        List<PictureBox> enemyBullets = new();

        int MunitionSpeed;
        int backgroundSpeed;
        Random rnd;
        PictureBox player;
        int playerSpeed;

        // Added: keep the loaded image as a field so we can reuse and dispose it
        private Image? munitionImage;
        private Image?[] enemyImages;
        private Image? starImage;

        // UI / gameplay state
        private Label scoreLabel;
        private Label levelLabel;
        private int score = 0;
        private int level = 1;
        private int enemiesKilledThisLevel = 0;
        private bool starActive = false;
        private PictureBox? starPowerup = null;
        private int bossesRemaining = 0;

        // Wave-based spawner state
        private int waveNumber = 0;
        private int enemiesRemainingInWave = 0;

        public Form1()
        {
            InitializeComponent();
            // register for disposal of loaded assets
            this.FormClosed += Form1_FormClosed;
        }

        //initialize game elements
        private void Form1_Load(object sender, EventArgs e)
        {
            player = Player; // link the fields
            playerSpeed = 5;
            backgroundSpeed = 4;
            stars = new PictureBox[10];
            rnd = new Random();

            MunitionSpeed = 20;
            munitions = new PictureBox[3];

            // explicit user-provided locations (preferred)
            string explicitAsserts = @"C:\Users\ricky\Desktop\own work\spacewar\spacewar\bin\Debug\asserts";
            string explicitSongs = @"C:\Users\ricky\Desktop\own work\spacewar\spacewar\bin\Debug\songs";

            // resolve asset base directories: prefer explicit paths if they exist, otherwise fall back to AppContext.BaseDirectory variants
            string baseDir = AppContext.BaseDirectory;
            string assertsDir = Directory.Exists(explicitAsserts) ? explicitAsserts : Path.Combine(baseDir, "asserts");
            string songsDir = Directory.Exists(explicitSongs) ? explicitSongs : Path.Combine(baseDir, "songs");

            // create WMPs
            gameMedia = new WindowsMediaPlayer();
            shootMedia = new WindowsMediaPlayer();
            boomMedia = new WindowsMediaPlayer();

            // load songs if available
            string gameSongPath = Path.Combine(songsDir, "GameSong.mp3");
            string shootPath = Path.Combine(songsDir, "shoot.mp3");
            string boomPath = Path.Combine(songsDir, "boom.mp3");

            if (File.Exists(gameSongPath))
            {
                gameMedia.URL = gameSongPath;
                gameMedia.settings.setMode("loop", true);
                gameMedia.settings.volume = 30;
                try { gameMedia.controls.play(); } catch { }
            }

            if (File.Exists(shootPath))
            {
                shootMedia.URL = shootPath;
                shootMedia.settings.volume = 50;
            }

            if (File.Exists(boomPath))
            {
                boomMedia.URL = boomPath;
                boomMedia.settings.volume = 60;
            }

            //load images for munitions and logic/design (defensive path resolution)
            string munitionPath = Path.Combine(assertsDir, "munition.png");
            if (!File.Exists(munitionPath))
            {
                // try sibling folder (if your working dir differs)
                munitionPath = Path.GetFullPath(Path.Combine(baseDir, "..", "asserts", "munition.png"));
            }

            if (File.Exists(munitionPath))
            {
                munitionImage = Image.FromFile(munitionPath);
            }
            else
            {
                // Fallback: create a visible placeholder so the app continues running
                Bitmap placeholder = new Bitmap(8, 8);
                using (Graphics g = Graphics.FromImage(placeholder))
                {
                    g.Clear(Color.Magenta); // obvious placeholder color
                }
                munitionImage = placeholder;
            }

            // load enemy images E1/E2/E3 (use placeholders if missing)
            enemyImages = new Image?[3];
            for (int i = 1; i <= 3; i++)
            {
                string ePath = Path.Combine(assertsDir, $"E{i}.png");
                if (File.Exists(ePath))
                {
                    enemyImages[i - 1] = Image.FromFile(ePath);
                }
                else
                {
                    // simple placeholder
                    Bitmap p = new Bitmap(40, 30);
                    using (Graphics g = Graphics.FromImage(p))
                    {
                        g.Clear(Color.FromArgb(200, 0, 0));
                        g.DrawString($"E{i}", SystemFonts.DefaultFont, Brushes.White, 4, 8);
                    }
                    enemyImages[i - 1] = p;
                }
            }

            // load star image if present
            string starPath = Path.Combine(assertsDir, "star.png");
            if (File.Exists(starPath))
            {
                starImage = Image.FromFile(starPath);
            }
            else
            {
                // tiny placeholder
                Bitmap s = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(s))
                {
                    g.Clear(Color.Gold);
                }
                starImage = s;
            }

            // create UI labels
            scoreLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(8, 8),
                Text = "Score: 0"
            };
            levelLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(8, 28),
                Text = "Level: 1"
            };
            this.Controls.Add(scoreLabel);
            this.Controls.Add(levelLabel);

            // create munition boxes (hidden initially)
            for (int i = 0; i < munitions.Length; i++)
            {
                munitions[i] = new PictureBox();
                munitions[i].Size = new Size(8, 8);
                munitions[i].SizeMode = PictureBoxSizeMode.Zoom;
                munitions[i].BorderStyle = BorderStyle.None;
                munitions[i].Image = munitionImage;
                // start hidden and off-screen until fired
                munitions[i].Visible = false;
                munitions[i].Location = new Point(-100, -100);
                this.Controls.Add(munitions[i]);
            }

            // create a small pool of enemy bullets
            for (int i = 0; i < 12; i++)
            {
                var b = new PictureBox
                {
                    Size = new Size(6, 12),
                    BackColor = Color.OrangeRed,
                    Visible = false,
                    Location = new Point(-100, -100)
                };
                enemyBullets.Add(b);
                this.Controls.Add(b);
            }

            // spawn initial wave
            StartWave(6 + level * 2);

            //create background stars
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = new PictureBox();
                stars[i].BorderStyle = BorderStyle.None;
                stars[i].Location = new Point(rnd.Next(20, 500), rnd.Next(-10, 400));
                if (i % 2 == 1)
                {
                    stars[i].Size = new Size(2, 2);
                    stars[i].BackColor = Color.Wheat;
                }
                else
                {
                    stars[i].Size = new Size(3, 3);
                    stars[i].BackColor = Color.DarkGray;
                }
                this.Controls.Add(stars[i]);
            }

            // ensure MoveMunitionTimer is enabled (designer created timer)
            MoveMunitionTimer.Interval = 20;
            MoveMunitionTimer.Enabled = true;
        }

        // Dispose the loaded image when the form closes
        private void Form1_FormClosed(object? sender, FormClosedEventArgs e)
        {
            if (munitionImage != null)
            {
                munitionImage.Dispose();
                munitionImage = null;
            }

            if (enemyImages != null)
            {
                foreach (var img in enemyImages)
                {
                    img?.Dispose();
                }
            }

            starImage?.Dispose();

            // stop media
            try { gameMedia.controls.stop(); } catch { }
            try { shootMedia.controls.stop(); } catch { }
            try { boomMedia.controls.stop(); } catch { }
        }

        // Spawn N enemies (normal). Boss flag not used here.
        private void SpawnEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var e = new PictureBox
                {
                    Size = new Size(40, 30),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = enemyImages[rnd.Next(enemyImages.Length)],
                    Location = new Point(rnd.Next(10, Math.Max(60, this.ClientSize.Width - 50)), rnd.Next(-300, -30)),
                    Tag = "enemy" // simple tag
                };
                enemies.Add(e);
                this.Controls.Add(e);
            }
        }

        // Start a new wave of normal enemies
        private void StartWave(int enemyCount)
        {
            waveNumber++;
            enemiesRemainingInWave = enemyCount;
            SpawnEnemies(enemyCount);
        }

        // Spawn boss set after collecting star
        private void SpawnBosses(int count)
        {
            bossesRemaining = count;
            for (int i = 0; i < count; i++)
            {
                var b = new PictureBox
                {
                    Size = new Size(80, 60),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = enemyImages[rnd.Next(enemyImages.Length)], // reuse enemy art
                    Location = new Point(60 + (i * 90) % Math.Max(1, (this.ClientSize.Width - 120)), -100 - i * 120),
                    Tag = 5 + level // use Tag as health (boxed int)
                };
                enemies.Add(b);
                this.Controls.Add(b);
            }
        }

        //move background stars and all moving objects
        private void MoveBgTimer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < stars.Length / 2; i++)
            {
                stars[i].Top += backgroundSpeed;
                if (stars[i].Top > this.Height)
                {
                    stars[i].Top = -stars[i].Height;
                }
            }
            for (int i = stars.Length / 2; i < stars.Length; i++)
            {
                stars[i].Top += backgroundSpeed - 2;
                if (stars[i].Top > this.Height)
                {
                    stars[i].Top = -stars[i].Height;
                }
            }
        }

        //move left
        private void LeftMoveTimer_Tick(object sender, EventArgs e)
        {
            if (player.Left > 10)
            {
                player.Left -= playerSpeed;
            }
        }
        //move right
        private void RightMoveTimer_Tick(object sender, EventArgs e)
        {
            if (Player.Right < 580)
            {
                Player.Left += playerSpeed;
            }
        }
        //move down
        private void DownMoveTimer_Tick(object sender, EventArgs e)
        {
            if (player.Top < 400)
            {
                player.Top += playerSpeed;
            }
        }
        //move up
        private void UpMoveTimer_Tick(object sender, EventArgs e)
        {
            if (player.Top > 10)
            {
                player.Top -= playerSpeed;
            }

        }
        //start moving when key is pressed
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right)
            {
                RightMoveTimer.Start();
            }
            if (e.KeyCode == Keys.Left)
            {
                LeftMoveTimer.Start();
            }
            if (e.KeyCode == Keys.Down)
            {
                DownMoveTimer.Start();
            }
            if (e.KeyCode == Keys.Up)
            {
                UpMoveTimer.Start();
            }

            // Fire on Space
            if (e.KeyCode == Keys.Space)
            {
                Shoot();
            }
        }

        //stop moving when key is released
        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            RightMoveTimer.Stop();
            LeftMoveTimer.Stop();
            DownMoveTimer.Stop();
            UpMoveTimer.Stop();
        }


        //move munitions, enemies and bullets, handle collisions
        private void MoveMunitionTimer_Tick(object sender, EventArgs e)
        {
            // move player munitions
            for (int i = 0; i < munitions.Length; i++)
            {
                if (munitions[i].Visible)
                {
                    munitions[i].Top -= MunitionSpeed;

                    // collision with enemies
                    for (int j = enemies.Count - 1; j >= 0; j--)
                    {
                        var en = enemies[j];
                        if (en == null) continue;
                        if (munitions[i].Bounds.IntersectsWith(en.Bounds) && munitions[i].Visible && en.Visible)
                        {
                            // play boom (restart playback)
                            try { boomMedia.controls.stop(); boomMedia.controls.play(); } catch { }

                            // check if boss (Tag is boxed int health)
                            if (en.Tag is int health)
                            {
                                health -= 1;
                                en.Tag = health;
                                // destroy the munition
                                munitions[i].Visible = false;
                                munitions[i].Location = new Point(-100, -100);

                                if (health <= 0)
                                {
                                    // boss defeated
                                    enemies.RemoveAt(j);
                                    this.Controls.Remove(en);
                                    en.Dispose();
                                    bossesRemaining--;
                                    score += 10 * level;
                                    scoreLabel.Text = $"Score: {score}";
                                }
                            }
                            else
                            {
                                // normal enemy: remove
                                enemies.RemoveAt(j);
                                this.Controls.Remove(en);
                                en.Dispose();

                                munitions[i].Visible = false;
                                munitions[i].Location = new Point(-100, -100);

                                score += 1;
                                enemiesKilledThisLevel++;
                                scoreLabel.Text = $"Score: {score}";

                                // decrement wave counter (wave-based spawner)
                                enemiesRemainingInWave = Math.Max(0, enemiesRemainingInWave - 1);
                            }

                            // check level objective
                            if (!starActive && enemiesKilledThisLevel >= 30)
                            {
                                SpawnStarPowerup();
                            }

                            break; // munition used up
                        }
                    }

                    // if it went off the top, deactivate it
                    if (munitions[i].Bottom < 0)
                    {
                        munitions[i].Visible = false;
                        munitions[i].Location = new Point(-100, -100);
                    }
                }
            }

            // move enemies and occasionally shoot
            int enemySpeed = 1 + level / 2;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var en = enemies[i];
                if (en == null || !en.Visible) continue;

                // If en is a boss (Tag is int health), move slower upward-to-down or oscillate
                if (en.Tag is int)
                {
                    // bosses move slower vertically, slight horizontal oscillation
                    en.Top += Math.Max(1, enemySpeed - 1);
                    en.Left += (int)(Math.Sin((DateTime.Now.Ticks / 500000 + i) % 10) * 1.5);
                }
                else
                {
                    en.Top += enemySpeed;
                }

                // wrap or respawn if off bottom (non-boss)
                if (en.Top > this.ClientSize.Height + 50 && !(en.Tag is int))
                {
                    en.Location = new Point(rnd.Next(10, this.ClientSize.Width - en.Width), rnd.Next(-200, -30));
                }

                // enemy shooting: chance based on level
                if (rnd.NextDouble() < 0.005 * level)
                {
                    FireEnemyBulletFrom(en);
                }

                // collision enemy with player (simple game over placeholder: deduct score)
                if (en.Bounds.IntersectsWith(Player.Bounds))
                {
                    score = Math.Max(0, score - 5);
                    scoreLabel.Text = $"Score: {score}";
                    // push enemy away
                    en.Location = new Point(rnd.Next(10, this.ClientSize.Width - en.Width), rnd.Next(-200, -30));
                }
            }

            // move enemy bullets
            for (int i = 0; i < enemyBullets.Count; i++)
            {
                var b = enemyBullets[i];
                if (!b.Visible) continue;
                b.Top += 4 + level; // faster each level

                if (b.Bounds.IntersectsWith(Player.Bounds))
                {
                    // hit player - penalize a bit
                    score = Math.Max(0, score - 2);
                    scoreLabel.Text = $"Score: {score}";
                    b.Visible = false;
                    b.Location = new Point(-100, -100);
                }
                if (b.Top > this.ClientSize.Height)
                {
                    b.Visible = false;
                    b.Location = new Point(-100, -100);
                }
            }

            // star powerup collision with player
            if (starActive && starPowerup != null && starPowerup.Visible && starPowerup.Bounds.IntersectsWith(Player.Bounds))
            {
                // collect star -> spawn bosses
                starPowerup.Visible = false;
                this.Controls.Remove(starPowerup);
                starPowerup.Dispose();
                starPowerup = null;
                starActive = false;

                // spawn bosses (5 plus level-based increase)
                int bossCount = 5 + (level - 1) * 1;
                SpawnBosses(bossCount);
            }

            // wave progression: when wave cleared and no bosses active, start next wave
            if (enemiesRemainingInWave == 0 && bossesRemaining == 0 && !starActive && enemies.All(e => !(e.Tag is int)))
            {
                // next wave: increase wave count and spawn more enemies (scales with level and wave)
                int nextWaveCount = 4 + level * 2 + waveNumber; // simple scaling
                StartWave(nextWaveCount);
            }

            // if all bosses defeated and none active, advance level
            if (bossesRemaining == 0 && enemiesKilledThisLevel >= 30 && enemies.All(e => !(e.Tag is int)))
            {
                // level complete
                level++;
                enemiesKilledThisLevel = 0;
                levelLabel.Text = $"Level: {level}";

                // after level up start a fresh wave sized for the new level
                StartWave(4 + level * 2);
            }
        }

        // Fire a single available munition from the player position
        private void Shoot()
        {
            for (int i = 0; i < munitions.Length; i++)
            {
                if (!munitions[i].Visible)
                {
                    // position munition centered on player (tweak offsets as needed)
                    int x = Player.Location.X + (Player.Width / 2) - (munitions[i].Width / 2);
                    int y = Player.Location.Y - munitions[i].Height; // just above the player
                    munitions[i].Location = new Point(x, y);
                    munitions[i].Visible = true;

                    // play shoot sound (restart)
                    try
                    {
                        shootMedia.controls.stop();
                        shootMedia.controls.play();
                    }
                    catch { }

                    break;
                }
            }
        }

        // Fire an enemy bullet from an enemy picturebox (reuses pool)
        private void FireEnemyBulletFrom(PictureBox enemy)
        {
            var b = enemyBullets.FirstOrDefault(bb => !bb.Visible);
            if (b == null) return;
            b.Location = new Point(enemy.Left + enemy.Width / 2 - b.Width / 2, enemy.Top + enemy.Height);
            b.Visible = true;
        }

        // Spawn a star powerup somewhere on screen
        private void SpawnStarPowerup()
        {
            if (starActive) return;
            starActive = true;
            starPowerup = new PictureBox
            {
                Size = new Size(20, 20),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = starImage,
                Location = new Point(rnd.Next(40, this.ClientSize.Width - 40), rnd.Next(40, this.ClientSize.Height / 2)),
                Visible = true
            };
            this.Controls.Add(starPowerup);
        }
    }
}