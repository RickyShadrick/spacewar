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
        List<Enemy> enemies = new();
        List<PictureBox> enemyBullets = new();

        int MunitionSpeed;
        int backgroundSpeed;
        Random rnd;
        PictureBox player;
        int playerSpeed;

        // player lives
        private int playerLives = 3;
        private Label livesLabel;
        private bool invulnerable = false;
        private int invulnerableTicks = 0;

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
        private bool starActive = false; // boss-star active
        private PictureBox? starPowerup = null; // boss-star
        private int bossesRemaining = 0;

        // life-star state
        private bool lifeStarActive = false;
        private PictureBox? lifeStarPowerup = null;
        private int totalKills = 0;
        private int nextLifeThreshold = 50;

        // Wave-based spawner state
        private int waveNumber = 0;
        private int enemiesRemainingInWave = 0;

        public Form1()
        {
            InitializeComponent();
            // register for disposal of loaded assets
            this.FormClosed += Form1_FormClosed;
            this.KeyPreview = true; // allow form to receive key events even if controls have focus
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

            // load star image if present (used for boss-star and life-star)
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
            livesLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.Yellow,
                BackColor = Color.Transparent,
                Location = new Point(8, 48),
                Text = $"Lives: {playerLives}"
            };
            this.Controls.Add(scoreLabel);
            this.Controls.Add(levelLabel);
            this.Controls.Add(livesLabel);

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

            // create a larger pool of enemy bullets
            for (int i = 0; i < 30; i++)
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

        // Spawn N enemies (normal). Now creates Enemy objects with non-overlapping spawn
        private void SpawnEnemies(int count)
        {
            int attemptsLimit = 30;
            for (int i = 0; i < count; i++)
            {
                var type = (EnemyType)rnd.Next(0, 4); // Straight, Sine, Zigzag, Homing
                var img = enemyImages[rnd.Next(enemyImages.Length)] ?? new Bitmap(40, 30);
                Point start;
                int attempts = 0;
                do
                {
                    start = new Point(rnd.Next(10, Math.Max(60, this.ClientSize.Width - 50)), rnd.Next(-300, -30));
                    attempts++;
                }
                while (attempts < attemptsLimit && enemies.Any(en => Rectangle.Intersect(new Rectangle(start, img.Size), en.Sprite.Bounds).Width > 0));

                int health = 1 + level / 3;
                float speed = 1f + (float)rnd.NextDouble() * 1.5f + level * 0.15f;

                var en = new Enemy(type, img, start, health, speed, rnd, 10, this.ClientSize.Width - 60);
                en.OnFire = Enemy_OnFire;
                en.OnDestroyed = Enemy_OnDestroyed;
                enemies.Add(en);
                this.Controls.Add(en.Sprite);
            }
        }

        // Start a new wave of normal enemies
        private void StartWave(int enemyCount)
        {
            waveNumber++;
            enemiesRemainingInWave = enemyCount;
            SpawnEnemies(enemyCount);
        }

        // Spawn boss set after collecting boss-star
        private void SpawnBosses(int count)
        {
            bossesRemaining = count;
            for (int i = 0; i < count; i++)
            {
                var img = enemyImages[rnd.Next(enemyImages.Length)] ?? new Bitmap(80, 60);
                var start = new Point(60 + (i * 90) % Math.Max(1, (this.ClientSize.Width - 120)), -100 - i * 120);
                int health = 5 + level;
                float speed = 0.8f + level * 0.05f;

                var boss = new Enemy(EnemyType.Boss, img, start, health, speed, rnd, 10, this.ClientSize.Width - 60);
                boss.OnFire = Enemy_OnFire;
                boss.OnDestroyed = Enemy_OnDestroyed;
                enemies.Add(boss);
                this.Controls.Add(boss.Sprite);
            }
        }

        // Spawn life-star (gives +1 life)
        private void SpawnLifeStarPowerup()
        {
            if (lifeStarActive) return;
            lifeStarActive = true;
            lifeStarPowerup = new PictureBox
            {
                Size = new Size(20, 20),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = starImage,
                Location = new Point(rnd.Next(40, Math.Max(60, this.ClientSize.Width - 40)), rnd.Next(40, this.ClientSize.Height / 2)),
                Visible = true
            };
            this.Controls.Add(lifeStarPowerup);
        }

        //move background stars and all moving objects
        private void MoveBgTimer_Tick(object sender, EventArgs e)
        {
            // update invulnerability
            if (invulnerable)
            {
                invulnerableTicks--;
                if (invulnerableTicks <= 0)
                {
                    invulnerable = false;
                    Player.Visible = true;
                }
                else
                {
                    // blink player
                    Player.Visible = (invulnerableTicks / 5) % 2 == 0;
                }
            }

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
            if (e.KeyCode == Keys.Right) RightMoveTimer.Stop();
            if (e.KeyCode == Keys.Left) LeftMoveTimer.Stop();
            if (e.KeyCode == Keys.Down) DownMoveTimer.Stop();
            if (e.KeyCode == Keys.Up) UpMoveTimer.Stop();
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
                        if (munitions[i].Bounds.IntersectsWith(en.Sprite.Bounds) && munitions[i].Visible && en.Sprite.Visible)
                        {
                            // play boom (restart playback)
                            try { boomMedia.controls.stop(); boomMedia.controls.play(); } catch { }

                            // damage enemy
                            en.Damage(1);

                            // destroy the munition
                            munitions[i].Visible = false;
                            munitions[i].Location = new Point(-100, -100);

                            // If enemy is normal, update kill counters handled in OnDestroyed

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

            // update enemies (movement and off-screen handling)
            UpdateEnemies();

            // enemy bullets movement and collisions
            int bulletSpeed = 4 + level; // scale bullet speed with level
            for (int i = 0; i < enemyBullets.Count; i++)
            {
                var b = enemyBullets[i];
                if (!b.Visible) continue;
                b.Top += bulletSpeed; // faster each level

                if (!invulnerable && b.Bounds.IntersectsWith(Player.Bounds))
                {
                    // hit player - lose a life
                    b.Visible = false;
                    b.Location = new Point(-100, -100);
                    PlayerHit();
                }
                else if (b.Top > this.ClientSize.Height)
                {
                    b.Visible = false;
                    b.Location = new Point(-100, -100);
                }
            }

            // life-star collision with player
            if (lifeStarActive && lifeStarPowerup != null && lifeStarPowerup.Visible && lifeStarPowerup.Bounds.IntersectsWith(Player.Bounds))
            {
                // collect life star -> grant life
                lifeStarPowerup.Visible = false;
                this.Controls.Remove(lifeStarPowerup);
                lifeStarPowerup.Dispose();
                lifeStarPowerup = null;
                lifeStarActive = false;

                playerLives++;
                livesLabel.Text = $"Lives: {playerLives}";

                // increase next threshold
                nextLifeThreshold += 50;
            }

            // star powerup (boss-star) collision with player
            if (starActive && starPowerup != null && starPowerup.Visible && starPowerup.Bounds.IntersectsWith(Player.Bounds))
            {
                // collect boss-star -> spawn bosses
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
            if (enemiesRemainingInWave == 0 && bossesRemaining == 0 && !starActive && enemies.All(e => e.Type != EnemyType.Boss) && enemies.Count == 0)
            {
                // next wave: increase wave count and spawn more enemies (scales with level and wave)
                int nextWaveCount = 4 + level * 2 + waveNumber; // simple scaling
                StartWave(nextWaveCount);
            }

            // if all bosses defeated and none active, advance level
            if (bossesRemaining == 0 && enemiesKilledThisLevel >= 30 && enemies.All(e => e.Type != EnemyType.Boss))
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

        // Fire an enemy bullet from an enemy (reuses pool)
        private void FireEnemyBulletFromEnemy(Enemy enemy)
        {
            var b = enemyBullets.FirstOrDefault(bb => !bb.Visible);
            if (b == null) return;
            b.Location = new Point(enemy.Sprite.Left + enemy.Sprite.Width / 2 - b.Width / 2, enemy.Sprite.Top + enemy.Sprite.Height);
            b.Visible = true;
        }

        // Enemy requested to fire
        private void Enemy_OnFire(Enemy en)
        {
            FireEnemyBulletFromEnemy(en);
        }

        // Called when enemy reports destroyed
        private void Enemy_OnDestroyed(Enemy en)
        {
            if (!enemies.Contains(en)) return;
            bool isBoss = en.Type == EnemyType.Boss;

            enemies.Remove(en);
            try { this.Controls.Remove(en.Sprite); } catch { }
            en.Dispose();

            if (isBoss)
            {
                bossesRemaining = Math.Max(0, bossesRemaining - 1);
                score += 10 * level;
            }
            else
            {
                enemiesKilledThisLevel++;
                enemiesRemainingInWave = Math.Max(0, enemiesRemainingInWave - 1);
                score += 1;

                // increment cumulative kills and check life-star threshold
                totalKills++;
                if (!lifeStarActive && totalKills >= nextLifeThreshold)
                {
                    SpawnLifeStarPowerup();
                }
            }

            scoreLabel.Text = $"Score: {score}";

            if (!starActive && enemiesKilledThisLevel >= 30)
            {
                // spawn boss-star to trigger bosses collection (unchanged)
                SpawnStarPowerup();
            }
        }

        // Update enemies: movement, off-screen removal and player collisions
        private void UpdateEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var en = enemies[i];
                en.Update(Player.Location, level);

                // collision enemy with player
                if (!invulnerable && en.Sprite.Bounds.IntersectsWith(Player.Bounds))
                {
                    // Determine if this enemy type is non-shooting -> instant kill/impact
                    bool nonShooting = en.Type == EnemyType.Straight || en.Type == EnemyType.Sine || en.Type == EnemyType.Patrol;
                    if (nonShooting)
                    {
                        // remove the enemy and kill player
                        enemies.RemoveAt(i);
                        try { this.Controls.Remove(en.Sprite); } catch { }
                        en.Dispose();
                        enemiesRemainingInWave = Math.Max(0, enemiesRemainingInWave - 1);

                        PlayerHit();
                        continue;
                    }
                    else
                    {
                        // for shooting enemies, penalize and push away (as earlier)
                        score = Math.Max(0, score - 5);
                        scoreLabel.Text = $"Score: {score}";
                        en.Sprite.Location = new Point(rnd.Next(10, this.ClientSize.Width - en.Sprite.Width), rnd.Next(-200, -30));
                    }
                }

                // remove if off-screen (for normal enemies)
                if (en.Sprite.Top > this.ClientSize.Height + 60 && en.Type != EnemyType.Boss)
                {
                    enemies.RemoveAt(i);
                    try { this.Controls.Remove(en.Sprite); } catch { }
                    en.Dispose();
                    enemiesRemainingInWave = Math.Max(0, enemiesRemainingInWave - 1);
                }
            }
        }

        // handle player hit
        private void PlayerHit()
        {
            if (invulnerable) return;
            playerLives--;
            livesLabel.Text = $"Lives: {playerLives}";

            try { boomMedia.controls.stop(); boomMedia.controls.play(); } catch { }

            if (playerLives <= 0)
            {
                GameOver();
                return;
            }

            // respawn player and make invulnerable for a short time
            Player.Location = new Point(260, 400);
            invulnerable = true;
            invulnerableTicks = 80; // about 1.6s at 20ms tick
        }

        private void GameOver()
        {
            // stop timers
            MoveBgTimer.Stop();
            LeftMoveTimer.Stop();
            RightMoveTimer.Stop();
            UpMoveTimer.Stop();
            DownMoveTimer.Stop();
            MoveMunitionTimer.Stop();
            // stop music
            try { gameMedia.controls.stop(); } catch { }

            MessageBox.Show($"Game Over\nScore: {score}", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // restart simple: close
            this.Close();
        }

        // Spawn a star powerup somewhere on screen (boss-star)
        private void SpawnStarPowerup()
        {
            if (starActive) return;
            starActive = true;
            starPowerup = new PictureBox
            {
                Size = new Size(20, 20),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = starImage,
                Location = new Point(rnd.Next(40, Math.Max(60, this.ClientSize.Width - 40)), rnd.Next(40, this.ClientSize.Height / 2)),
                Visible = true
            };
            this.Controls.Add(starPowerup);
        }
    }
}