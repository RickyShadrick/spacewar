using System;
using System.Drawing;
using System.Windows.Forms;

namespace spacewar
{
    public enum EnemyType
    {
        Straight,
        Sine,
        Zigzag,
        Homing,
        Patrol,
        Boss
    }

    public class Enemy : IDisposable
    {
        public PictureBox Sprite { get; }
        public EnemyType Type { get; }
        public int Health { get; set; }
        public float Speed { get; set; }
        public Action<Enemy>? OnFire { get; set; }
        public Action<Enemy>? OnDestroyed { get; set; }

        int ageTicks;
        readonly int initialX;
        readonly float sineAmp;
        readonly float sineFreq;
        int zigDir = 1;
        int zigStep = 0;
        int patrolDir = 1;
        int patrolLeftBound;
        int patrolRightBound;
        readonly Random rnd;

        public Enemy(EnemyType type, Image image, Point startPosition, int health, float speed, Random rnd,
                     int patrolLeft = 0, int patrolRight = 0)
        {
            Type = type;
            Health = health;
            Speed = speed;
            this.rnd = rnd;
            Sprite = new PictureBox
            {
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(image?.Width > 0 ? Math.Min(image.Width, 80) : 40, image?.Height > 0 ? Math.Min(image.Height, 60) : 30),
                Location = startPosition,
                BackColor = Color.Transparent
            };

            initialX = startPosition.X;
            sineAmp = rnd.Next(10, 40);
            sineFreq = (float)(rnd.NextDouble() * 0.12 + 0.03);
            patrolLeftBound = patrolLeft;
            patrolRightBound = patrolRight == 0 ? patrolLeft + 200 : patrolRight;
        }

        public void Update(Point playerPosition, int level)
        {
            ageTicks++;
            float levelMultiplier = 1f + level * 0.08f;

            switch (Type)
            {
                case EnemyType.Straight:
                    Sprite.Top += (int)Math.Ceiling(Speed * levelMultiplier);
                    break;
                case EnemyType.Sine:
                    Sprite.Top += (int)Math.Ceiling(Speed * levelMultiplier);
                    Sprite.Left = initialX + (int)(Math.Sin(ageTicks * sineFreq) * sineAmp);
                    break;
                case EnemyType.Zigzag:
                    Sprite.Top += (int)Math.Ceiling(Speed * levelMultiplier);
                    Sprite.Left += 2 * zigDir;
                    zigStep++;
                    if (zigStep > 20)
                    {
                        zigStep = 0;
                        zigDir = -zigDir;
                    }
                    break;
                case EnemyType.Homing:
                    {
                        var dx = playerPosition.X - Sprite.Left;
                        var dy = playerPosition.Y - Sprite.Top;
                        var mag = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
                        Sprite.Left += (int)Math.Round((dx / mag) * Speed * levelMultiplier * 0.6);
                        Sprite.Top += (int)Math.Round((dy / mag) * Speed * levelMultiplier * 0.6);
                        break;
                    }
                case EnemyType.Patrol:
                    Sprite.Left += (int)Math.Round(Speed * patrolDir * levelMultiplier);
                    if (Sprite.Left < patrolLeftBound || Sprite.Left > patrolRightBound)
                    {
                        patrolDir = -patrolDir;
                    }
                    Sprite.Top += (int)Math.Ceiling((Speed / 4) * levelMultiplier);
                    break;
                case EnemyType.Boss:
                    Sprite.Top += (int)Math.Max(1, Math.Ceiling((Speed / 2) * levelMultiplier));
                    Sprite.Left = initialX + (int)(Math.Sin(ageTicks * (sineFreq / 2)) * (sineAmp + 40));
                    if (ageTicks % Math.Max(40, 120 - level * 6) == 0)
                    {
                        OnFire?.Invoke(this);
                    }
                    break;
            }

            if ((Type == EnemyType.Homing || Type == EnemyType.Zigzag) && ageTicks % Math.Max(200, 1000 - level * 40) == 0)
            {
                OnFire?.Invoke(this);
            }
        }

        public void Damage(int amount)
        {
            Health -= amount;
            if (Health <= 0)
            {
                OnDestroyed?.Invoke(this);
            }
        }

        public void Dispose()
        {
            try { Sprite?.Dispose(); } catch { }
        }
    }
}
