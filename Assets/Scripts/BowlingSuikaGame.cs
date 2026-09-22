using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DopamineSlayer
{
    public static class BowlingSuikaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateGame(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CreateGame(scene);

        private static void CreateGame(Scene scene)
        {
            if (scene.name != "SampleScene") return;
            if (Object.FindFirstObjectByType<BowlingSuikaGame>() != null) return;
            new GameObject("Bowling Suika Game").AddComponent<BowlingSuikaGame>();
        }
    }

    public static class HomeSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            CreateHomeScreen(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CreateHomeScreen(scene);

        private static void CreateHomeScreen(Scene scene)
        {
            if (scene.name != "HomeScene") return;
            if (Object.FindFirstObjectByType<HomeSceneUI>() != null) return;
            new GameObject("Home Scene UI").AddComponent<HomeSceneUI>();
        }
    }

    // PlayerPrefs works in standalone builds and WebGL (per browser/device).
    // A shared online leaderboard needs a server; see the project handoff notes.
    public static class PlayerProfile
    {
        // The player name belongs to the current play session only; it is not saved on disk.
        private static string currentName = string.Empty;
        public static string Name => currentName;

        public static void SetName(string value)
        {
            var cleanName = (value ?? string.Empty).Trim();
            if (cleanName.Length > 12) cleanName = cleanName.Substring(0, 12);
            currentName = cleanName;
        }
    }

    // HomeScene의 난이도 설정은 현재 플레이 세션과 재시작에 계속 적용된다.
    public static class GameDifficultySettings
    {
        public static int StartingLives { get; private set; } = 3;
        public static bool InfiniteMode { get; private set; }

        public static void Apply(int startingLives, bool infiniteMode)
        {
            StartingLives = Mathf.Clamp(startingLives, 1, 3);
            InfiniteMode = infiniteMode;
        }
    }

    public static class SoundSettings
    {
        public static float BgmVolume { get; set; } = 1f;
        public static float SfxVolume { get; set; } = 1f;
        public static void Apply()
        {
            BowlingAudio.ApplyVolumes();
            HomeBackgroundMusic.ApplyVolume();
        }
    }

    public static class LocalLeaderboard
    {
        private const string ScoresKey = "BeadBowling.LocalLeaderboard";
        private const int MaxEntries = 5;

        public static void AddScore(string playerName, int score)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return;
            var entries = Load();
            // A tie never replaces or adds a record: only a distinct score can update the top five.
            if (entries.Any(entry => entry.Score == score)) return;
            entries.Add(new ScoreEntry { Name = playerName.Replace("|", string.Empty).Replace(":", string.Empty), Score = score });
            entries.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            var saved = string.Empty;
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0) saved += "|";
                saved += entries[i].Name + ":" + entries[i].Score;
            }
            PlayerPrefs.SetString(ScoresKey, saved);
            PlayerPrefs.Save();
        }

        public static List<ScoreEntry> Load()
        {
            var entries = new List<ScoreEntry>();
            var saved = PlayerPrefs.GetString(ScoresKey, string.Empty);
            foreach (var rawEntry in saved.Split('|'))
            {
                var separator = rawEntry.LastIndexOf(':');
                if (separator <= 0) continue;
                if (int.TryParse(rawEntry.Substring(separator + 1), out var score))
                    entries.Add(new ScoreEntry { Name = rawEntry.Substring(0, separator), Score = score });
            }
            entries.Sort((a, b) => b.Score.CompareTo(a.Score));
            return entries;
        }

        public struct ScoreEntry { public string Name; public int Score; }
    }

    public sealed class BowlingSuikaGame : MonoBehaviour
    {
        public static readonly Color[] BallColors =
        {
            new Color(0.95f, 0.16f, 0.15f), new Color(1f, 0.42f, 0.08f), new Color(1f, 0.85f, 0.12f),
            new Color(0.18f, 0.78f, 0.28f), new Color(0.15f, 0.5f, 0.96f), new Color(0.12f, 0.18f, 0.52f),
            new Color(0.56f, 0.2f, 0.83f), new Color(0.025f, 0.025f, 0.035f), new Color(0.55f, 0.9f, 1f)
        };
        public static readonly string[] BallNames = { "RED", "ORANGE", "YELLOW", "GREEN", "BLUE", "NAVY", "PURPLE", "BLACK HOLE", "LIGHTNING" };
        public const int BlackHoleTier = 7;
        public const int LightningTier = 8;

        private static Sprite circleSprite;
        private static Sprite squareSprite;
        private static Sprite glossyMarbleSprite;
        private static Sprite[] planetSprites;
        private static GameObject[] planetPrefabs;
        private static Vector2[][] planetColliderOutlines;
        private static Font pixelFont;
        // 게임 진행 순서. 3단계와 4단계는 원본 Prefab 번호를 서로 교환한다.
        private static readonly int[] PlanetPrefabOrder = { 0, 1, 2, 4, 3, 5, 6, 7 };
        private static Sprite dirtBackgroundSprite;
        private static Sprite spaceStationWallSprite;
        private static Sprite ufoEventSprite;
        private static Sprite sunflowerSeedSprite;
        private static Sprite victoryRainbowSunflowerSeedSprite;
        private static Sprite victoryRainbowHaloSprite;
        private static Sprite sunflowerExplosionSprite;
        private static Sprite blackHoleRingSprite;
        private static Sprite lightningBallSprite;
        private static Sprite topTileMonsterSprite;
        private static Sprite[] topTileMonsterWalkSprites;
        private static Sprite[] lightningChainBeamSprites;
        // The player selected beam C as the single visual used by every chain link.
        private const int LightningChainBeamIndex = 2;
        // World-space height of the beam. Raise this value to make the beam thicker.
        private const float LightningChainBeamHeight = 1f;
        private const float LauncherMoveLimit = 3.95f;
        private const float LauncherMoveSpeed = 5.2f;
        // The original launcher position is the top of its new vertical range.
        private const float LauncherVerticalTop = -4.0f;
        // Keep the ball just inside the bottom OUT line while still allowing a low launch.
        private const float LauncherVerticalBottom = -5.45f;
        private const float LauncherVerticalMoveSpeed = 3.6f;
        // 기본 발사력입니다. 이 값은 최대 발사 속도를 결정합니다.
        private const float NormalLaunchImpulse = 14.4f;
        // 블랙홀은 일반 공보다 절반 속도로 발사됩니다.
        private const float BlackHoleLaunchSpeedMultiplier = 0.7f;
        // 2배 감도: 마우스를 기존 최대 거리의 절반만 당겨도 최대 발사력이 됩니다.
        private const float PullSensitivity = 2f;
        private const float MaxLaunchPull = 2.7f;
        // 최대 당김(2.7 / 2 = 1.35)에서도 커서가 OUT 선 위에 있도록 발사대를 올립니다.
        private Vector2 launcher = new Vector2(0f, LauncherVerticalTop);
        private bool dragging;
        private int selectedTier;
        private int followingTier = -1;
        private int afterFollowingTier = -1;
        private float nextThrowTime;
        // 발사 후 다음 공을 보여 주고 다시 조작할 수 있게 될 때까지의 재장전 시간입니다.
        private const float ReloadDelaySeconds = 0.1f;
        // Normal game starts from zero. Change only when intentionally testing a score milestone.
        private const int StartingScore = 0;
        private int score = StartingScore;
        private bool gameOver;
        private int maxLives;
        private int lives;
        private const int WallTileCount = 12;
        private const int UfoEventScoreStep = 2000;
        // Normal mode: show destructible side tiles and enable the score-based hamster events.
        private const bool SideTilesAndUfoEnabled = true;
        // Reaching this score changes the lane into a 30-second top-tile boss battle.
        private const int BossScoreTarget = 50000;
        private const int BossMonsterCount = 12;
        private const int BossMonsterHitPoints = 64;
        // 50K에 도달했을 때 실제 보스전이 시작되기 전의 빨간 경보 연출 시간입니다.
        private const float BossWarningDurationSeconds = 4f;
        private const float BossDurationSeconds = 30f;
        private const int BossKillScore = 1000;
        // Change this value to resize every top-tile monster at once.
        private const float TileMonsterScale = 2f;
        // Boss-only hamster support fire. One attack is started every three seconds.
        private const float BossUfoAttackInterval = 3f;
        private const int BossSunflowerDamage = 20;
        // 무지개 해바라기씨가 나타난 시점부터 GAME CLEAR를 표시하기까지의 시간입니다.
        private const float BossClearSeedDisplaySeconds = 6f;
        // GAME CLEAR 때 무지개 광선이 해바라기씨를 조금 감싸는 크기입니다.
        // 더 크게 보이고 싶으면 이 값을 키우세요. (예: 1.00f)
        private const float VictoryRainbowSeedHaloScale = 0.78f;
        // UFO가 화면 밖에서 등장한 뒤 돌진을 시작하기 전 멈춰 있는 시간입니다.
        private const float UfoSpawnPauseSeconds = 0.34f;
        private const float UfoApproachDashDuration = 0.55f;
        // 해바라기씨 폭탄이 생성된 뒤 가속을 시작하기 전 정지 시간입니다.
        private const float SunflowerMissilePauseSeconds = 1f;
        private const float SunflowerExplosionDuration = 0.28f;
        private readonly List<GameObject> leftWallTiles = new List<GameObject>();
        private readonly List<GameObject> rightWallTiles = new List<GameObject>();
        private readonly List<GameObject> topWallTiles = new List<GameObject>();
        private readonly List<WallGap> brokenSideGaps = new List<WallGap>();
        private int nextUfoEventScore = UfoEventScoreStep;
        private int pendingUfoEvents;
        private bool ufoEventRunning;
        private readonly List<TileMonster> tileMonsters = new List<TileMonster>();
        private bool bossActive;
        private bool bossWarningActive;
        private bool gameClear;
        private bool bossClearSequenceRunning;
        private bool bossCountdownFrozen;
        private float bossStartedAt;
        private float bossWarningStartedAt;
        private float bossCountdownFrozenRemaining;
        private Coroutine bossCountdownRoutine;
        private LineRenderer aimLine;
        private Ball previewBall;
        public bool GameOver => gameOver;
        public int Score => score;
        public int Lives => lives;
        public int MaxLives => maxLives;
        public bool BossActive => bossActive;
        public bool BossWarningActive => bossWarningActive;
        public float BossWarningElapsed => bossWarningActive ? Mathf.Max(0f, Time.time - bossWarningStartedAt) : 0f;
        public bool GameClear => gameClear;
        public int BossEnemiesRemaining => tileMonsters.Count(monster => monster != null);
        public float BossTimeRemaining => bossCountdownFrozen
            ? bossCountdownFrozenRemaining
            : bossActive && !gameOver
                ? Mathf.Max(0f, BossDurationSeconds - (Time.time - bossStartedAt))
                : 0f;
        // Rounded upward: shows 30, then 29 after one full second has elapsed.
        public int BossCountdownSeconds => Mathf.CeilToInt(BossTimeRemaining);
        // Boss combat uses the complete visible background rather than the narrow bowling rail.
        public Rect BossArenaBounds
        {
            get
            {
                var camera = Camera.main;
                if (camera == null) return Rect.MinMaxRect(-10f, -6f, 10f, 6f);
                var halfHeight = camera.orthographicSize;
                var halfWidth = halfHeight * camera.aspect;
                return Rect.MinMaxRect(camera.transform.position.x - halfWidth, camera.transform.position.y - halfHeight,
                    camera.transform.position.x + halfWidth, camera.transform.position.y + halfHeight);
            }
        }
        // The currently selected ball is already visible at the launcher.
        public int NextTier => followingTier;
        public int FollowingTier => afterFollowingTier;
        public static Sprite CircleSprite => circleSprite;
        public static Sprite SquareSprite => squareSprite;
        public static Sprite GlossyMarbleSprite => glossyMarbleSprite;
        public static Sprite BlackHoleRingSprite => blackHoleRingSprite;
        public static Sprite GetTopTileMonsterWalkSprite(int frame)
        {
            if (topTileMonsterWalkSprites != null && topTileMonsterWalkSprites.Length == 2)
                return topTileMonsterWalkSprites[Mathf.Abs(frame) % 2];
            return topTileMonsterSprite;
        }
        public static Font PixelFont => pixelFont != null
            ? pixelFont
            : pixelFont = Resources.Load<Font>("Fonts/font1");
        public static Sprite GetBallSprite(int tier)
        {
            if (tier == LightningTier && lightningBallSprite != null) return lightningBallSprite;
            return planetSprites != null && tier >= 0 && tier < planetSprites.Length ? planetSprites[tier] : circleSprite;
        }
        public static float GetPrefabSpriteDiameter(int tier)
        {
            var prefabSprite = planetPrefabs != null && tier >= 0 && tier < planetPrefabs.Length && planetPrefabs[tier] != null
                ? planetPrefabs[tier].GetComponent<SpriteRenderer>()?.sprite
                : GetBallSprite(tier);
            if (prefabSprite == null) return 1f;
            return Mathf.Max(prefabSprite.bounds.size.x, prefabSprite.bounds.size.y);
        }
        public static Vector2[] GetBallColliderOutline(int tier)
        {
            // Saturn's ring projects well beyond its planet.  A generic circular collider
            // cuts those ends off, so use the outer silhouette of the planet-and-ring.
            if (tier == 6) return CreateSaturnOutline();
            if (planetColliderOutlines != null && tier >= 0 && tier < planetColliderOutlines.Length && planetColliderOutlines[tier] != null)
                return planetColliderOutlines[tier];
            return CreateCircularOutline(0.5f, 32);
        }
        public static float GetBallColliderExtent(int tier)
        {
            var extent = 0.5f;
            foreach (var point in GetBallColliderOutline(tier)) extent = Mathf.Max(extent, point.magnitude);
            return extent;
        }
        public static Sprite DirtBackgroundSprite => dirtBackgroundSprite;

        private void Awake()
        {
            // The HomeScene name field may leave IME composition enabled. Disable it in gameplay
            // so Q/W/A/S/D are always received as direct game controls.
            Input.imeCompositionMode = IMECompositionMode.Off;
            maxLives = GameDifficultySettings.StartingLives;
            lives = maxLives;
            // Dense, large planets need extra solver passes so their visual edges do not overlap.
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 12;
            CreateRuntimeSprites();
            if (Camera.main != null) Camera.main.gameObject.SetActive(false);
            CreateCamera();
            CreateLane();
            CreateAimLine();
            new GameObject("Game Audio").AddComponent<BowlingAudio>();
            gameObject.AddComponent<SoundSettingsUI>();
            SpawnPreview();
            gameObject.AddComponent<BowlingHUD>();
            gameObject.AddComponent<BlackHoleQPrompt>();
        }

        private void Update()
        {
            if (gameOver)
            {
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                if (Input.GetKeyDown(KeyCode.Escape)) SceneManager.LoadScene("HomeScene");
                return;
            }

            // R restarts the current SampleScene immediately, even before game over.
            if (Input.GetKeyDown(KeyCode.R))
            {
                Restart();
                return;
            }

            // A/D moves horizontally and W/S vertically, including while aiming. Horizontal
            // movement has priority so the launcher can never move diagonally from one input frame.
            var horizontal = 0f;
            if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D)) horizontal += 1f;
            var vertical = 0f;
            if (Input.GetKey(KeyCode.W)) vertical += 1f;
            if (Input.GetKey(KeyCode.S)) vertical -= 1f;
            if (horizontal != 0f)
            {
                launcher.x = Mathf.Clamp(launcher.x + horizontal * LauncherMoveSpeed * Time.deltaTime, -LauncherMoveLimit, LauncherMoveLimit);
                if (previewBall != null && !dragging) previewBall.transform.position = launcher;
            }
            else if (vertical != 0f)
            {
                launcher.y = Mathf.Clamp(launcher.y + vertical * LauncherVerticalMoveSpeed * Time.deltaTime, LauncherVerticalBottom, LauncherVerticalTop);
                if (previewBall != null && !dragging) previewBall.transform.position = launcher;
            }

            // Settings clicks and slider drags belong exclusively to the UI, never to the launcher.
            if (SoundSettingsUI.IsPointerOverInteractiveUI())
            {
                if (dragging) { dragging = false; aimLine.enabled = false; }
                return;
            }

            // Q activates every unused black hole that is still on the field.
            if (Input.GetKeyDown(KeyCode.Q))
            {
                var activatedAnyBlackHole = false;
                foreach (var blackHole in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None)
                             .Where(ball => ball.CanActivateBlackHoleRing))
                {
                    blackHole.ActivateBlackHoleRing();
                    activatedAnyBlackHole = true;
                }
                // Multiple black holes may activate together, but Q produces one sound.
                if (activatedAnyBlackHole)
                {
                    BowlingAudio.PlayBlackHoleAbility();
                    BlackHoleQPrompt.ShowPressedFrame();
                }
            }

            var mouse = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0) && Time.time >= nextThrowTime) dragging = true;
            if (dragging)
            {
                UpdateAim(mouse);
                if (Input.GetMouseButtonUp(0)) Launch(mouse);
            }
        }

        public void SelectBall(int tier)
        {
            if (gameOver || tier < 0 || tier > 2) return;
            selectedTier = tier;
            if (previewBall != null) previewBall.SetTier(tier);
        }

        public void AddScore(int tier)
        {
            score += (tier + 1) * 100;
            if (!GameDifficultySettings.InfiniteMode && !bossActive && !bossWarningActive && score >= BossScoreTarget)
            {
                BeginTopTileBossBattle();
                return;
            }
            if (GameDifficultySettings.InfiniteMode || bossActive || bossWarningActive || !SideTilesAndUfoEnabled) return;
            while (score >= nextUfoEventScore)
            {
                // Hamsters only remove the 24 left/right wall tiles.  The top rail is
                // never a target, and no more events are queued once both sides are empty.
                if (HasBreakableSideTiles()) pendingUfoEvents++;
                nextUfoEventScore += UfoEventScoreStep;
            }
            if (!ufoEventRunning && pendingUfoEvents > 0)
                StartCoroutine(RunUfoTileEvents());
        }

        private bool HasBreakableSideTiles() => leftWallTiles.Any(tile => tile != null) || rightWallTiles.Any(tile => tile != null);

        public void LoseLife(Ball escapedBall)
        {
            if (gameOver || escapedBall == null) return;
            Destroy(escapedBall.gameObject);
            // Once the top-tile battle begins, planets are ammunition rather than lives.
            if (bossActive) return;
            lives = Mathf.Max(0, lives - 1);
            BowlingAudio.PlayCoin();
            if (lives <= 0) EndGame();
        }

        // Lightning starts at the ball it struck, then searches outward from each
        // discovered ball.  Claiming a ball prevents a later search from selecting it
        // again while the visible expanding-radius scan is still running.
        private const float LightningSearchRadiusSpeed = 40f;
        private const float LightningSearchMaximumRadius = 16f;
        private const float LightningChainDeleteDelay = 0.5f;
        // Boss monsters are linked one by one so the path and its SFX read clearly.
        private const float LightningMonsterLinkInterval = 0.10f;
        // Start the clip slightly early because its audible impact begins after a short intro.
        private const float LightningExplosionSoundLeadTime = 0.12f;

        public void TriggerLightningChain(Ball struckBall)
        {
            if (struckBall == null || !struckBall.TryClaimForLightningChain()) return;
            StartCoroutine(ResolveLightningChain(struckBall));
        }

        public void TriggerLightningMonsterChain(TileMonster struckMonster)
        {
            if (!bossActive || struckMonster == null || struckMonster.IsDefeated) return;
            StartCoroutine(ResolveLightningMonsterChain(struckMonster));
        }

        private IEnumerator ResolveLightningMonsterChain(TileMonster firstMonster)
        {
            var targets = tileMonsters.Where(monster => monster != null && !monster.IsDefeated).ToList();
            if (!targets.Contains(firstMonster)) targets.Insert(0, firstMonster);
            var links = new List<GameObject>();
            var anchors = new List<GameObject>();
            // A low-HP first crab can disappear from its first 16 damage. Keep a
            // position anchor so its destruction never cuts the chain short.
            var previousPosition = firstMonster != null ? (Vector2)firstMonster.transform.position : Vector2.zero;
            if (firstMonster != null && !firstMonster.IsDefeated) firstMonster.TakeDamage(16);
            foreach (var target in targets)
            {
                if (target == null || target.IsDefeated || target == firstMonster) continue;
                var anchor = new GameObject("Lightning Monster Chain Anchor");
                anchor.transform.position = previousPosition;
                anchors.Add(anchor);
                var link = ShowLightningLink(anchor.transform, target.transform);
                if (link != null) links.Add(link);
                yield return new WaitForSeconds(LightningMonsterLinkInterval);
                if (target != null) previousPosition = target.transform.position;
                if (target != null && !target.IsDefeated) target.TakeDamage(16);
            }
            yield return new WaitForSeconds(LightningChainDeleteDelay);
            foreach (var link in links)
                if (link != null) Destroy(link);
            foreach (var anchor in anchors)
                if (anchor != null) Destroy(anchor);
            BowlingAudio.PlayLightningExplosion();
        }

        private IEnumerator ResolveLightningChain(Ball firstBall)
        {
            var chain = new List<Ball> { firstBall };
            var links = new List<GameObject>();
            var currentBall = firstBall;

            while (currentBall != null)
            {
                var nextBall = default(Ball);
                var center = (Vector2)currentBall.transform.position;

                // The search radius truly grows over time rather than checking the
                // entire field in one frame. The nearest first detection becomes the
                // centre of the next search.
                for (var radius = 0f; radius <= LightningSearchMaximumRadius; radius += LightningSearchRadiusSpeed * Time.deltaTime)
                {
                    nextBall = FindLightningChainCandidate(currentBall.Tier, center, radius);
                    if (nextBall != null && nextBall.TryClaimForLightningChain()) break;
                    nextBall = null;
                    yield return null;
                }

                if (nextBall == null) break;
                var link = ShowLightningLink(currentBall, nextBall);
                if (link != null) links.Add(link);
                chain.Add(nextBall);
                currentBall = nextBall;
            }

            yield return new WaitForSeconds(LightningChainDeleteDelay - LightningExplosionSoundLeadTime);
            // This always runs, including when the first struck ball found no partner.
            BowlingAudio.PlayLightningExplosion();
            yield return new WaitForSeconds(LightningExplosionSoundLeadTime);
            foreach (var link in links)
                if (link != null) Destroy(link);
            foreach (var ball in chain)
                if (ball != null) Destroy(ball.gameObject);
        }

        private static Ball FindLightningChainCandidate(int tier, Vector2 center, float radius)
        {
            Ball nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var candidate in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                if (candidate == null || !candidate.IsAvailableForLightningChain || candidate.Tier != tier) continue;
                var distance = Vector2.Distance(center, candidate.transform.position);
                if (distance <= radius && distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private static GameObject ShowLightningLink(Ball first, Ball second) =>
            ShowLightningLink(first != null ? first.transform : null, second != null ? second.transform : null);

        private static GameObject ShowLightningLink(Transform first, Transform second)
        {
            if (first == null || second == null || lightningChainBeamSprites == null) return null;
            var sprite = lightningChainBeamSprites[LightningChainBeamIndex];
            if (sprite == null) return null;

            var beam = new GameObject("Lightning Chain Beam");
            var renderer = beam.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10; // Always show above both connected planets.
            BowlingAudio.PlayLightning();
            beam.AddComponent<LightningChainBeam>().Configure(first, second, renderer, LightningChainBeamHeight);
            return beam;
        }

        private void EndGame(bool cleared = false)
        {
            if (gameOver) return;
            gameOver = true;
            bossWarningActive = false;
            gameClear = cleared;
            LocalLeaderboard.AddScore(PlayerProfile.Name, score);
            if (previewBall != null) previewBall.gameObject.SetActive(false);
            aimLine.enabled = false;
        }

        private void BeginTopTileBossBattle()
        {
            if (GameDifficultySettings.InfiniteMode || bossActive || bossWarningActive) return;
            bossWarningActive = true;
            bossWarningStartedAt = Time.time;
            // A pending side-wall hamster event must not overlap the boss alarm.
            pendingUfoEvents = 0;
            StopCoroutine(nameof(RunUfoTileEvents));
            BowlingAudio.PlayBossWarning();
            StartCoroutine(BeginTopTileBossBattleAfterWarning());
        }

        private IEnumerator BeginTopTileBossBattleAfterWarning()
        {
            // During this alarm, no monsters exist and the 30-second battle clock is not running yet.
            yield return new WaitForSeconds(BossWarningDurationSeconds);
            if (gameOver) yield break;
            bossWarningActive = false;
            bossActive = true;
            bossStartedAt = Time.time;
            bossCountdownFrozen = false;
            bossCountdownFrozenRemaining = BossDurationSeconds;
            // Discard the pre-boss preview queue so every new launch immediately uses
            // the complete 7-planet + special-ball boss probability table.
            if (previewBall != null) Destroy(previewBall.gameObject);
            followingTier = -1;
            afterFollowingTier = -1;
            SpawnPreview();
            var topTiles = topWallTiles.Where(tile => tile != null).ToList();
            foreach (var tile in topTiles)
            {
                var monster = CreateTopTileMonster(tile.transform.position);
                if (monster != null) tileMonsters.Add(monster);
                Destroy(tile);
            }
            topWallTiles.Clear();
            bossCountdownRoutine = StartCoroutine(BossCountdown());
            StartCoroutine(RunBossUfoAttacks());
        }

        private TileMonster CreateTopTileMonster(Vector2 position)
        {
            var monsterObject = new GameObject("Top Tile Monster");
            monsterObject.transform.position = position;
            monsterObject.transform.localScale = Vector3.one * TileMonsterScale;
            var renderer = monsterObject.AddComponent<SpriteRenderer>();
            renderer.sprite = topTileMonsterSprite != null ? topTileMonsterSprite : spaceStationWallSprite;
            renderer.sortingOrder = 3;
            var collider = monsterObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.68f, 0.56f);
            var body = monsterObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.drag = 0f;
            body.angularDrag = 0f;
            body.mass = 1000f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var monster = monsterObject.AddComponent<TileMonster>();
            monster.Configure(this, BossMonsterHitPoints);
            return monster;
        }

        private IEnumerator BossCountdown()
        {
            yield return new WaitForSeconds(BossDurationSeconds);
            bossCountdownRoutine = null;
            if (bossActive && !gameOver && !bossClearSequenceRunning) EndGame();
        }

        // During the boss phase, hamsters enter from a random edge and fire one
        // sunflower seed at a live monster every three seconds.
        private IEnumerator RunBossUfoAttacks()
        {
            while (bossActive && !gameOver)
            {
                yield return new WaitForSeconds(BossUfoAttackInterval);
                if (!bossActive || gameOver || BossEnemiesRemaining <= 0) yield break;
                yield return StartCoroutine(PlayBossUfoAttack());
            }
        }

        private IEnumerator PlayBossUfoAttack()
        {
            var targets = tileMonsters.Where(monster => monster != null && !monster.IsDefeated).ToList();
            if (targets.Count == 0) yield break;

            var target = targets[Random.Range(0, targets.Count)];
            var bounds = BossArenaBounds;
            var edge = Random.Range(0, 4); // left, right, bottom, top
            Vector2 outside;
            Vector2 hover;
            switch (edge)
            {
                case 0:
                    outside = new Vector2(bounds.xMin - 2f, Random.Range(bounds.yMin, bounds.yMax));
                    hover = new Vector2(bounds.xMin + 1.1f, outside.y);
                    break;
                case 1:
                    outside = new Vector2(bounds.xMax + 2f, Random.Range(bounds.yMin, bounds.yMax));
                    hover = new Vector2(bounds.xMax - 1.1f, outside.y);
                    break;
                case 2:
                    outside = new Vector2(Random.Range(bounds.xMin, bounds.xMax), bounds.yMin - 2f);
                    hover = new Vector2(outside.x, bounds.yMin + 1.1f);
                    break;
                default:
                    outside = new Vector2(Random.Range(bounds.xMin, bounds.xMax), bounds.yMax + 2f);
                    hover = new Vector2(outside.x, bounds.yMax - 1.1f);
                    break;
            }

            var ufo = new GameObject("Boss Hamster UFO");
            ufo.transform.position = new Vector3(outside.x, outside.y, -2f);
            var renderer = ufo.AddComponent<SpriteRenderer>();
            renderer.sprite = ufoEventSprite;
            renderer.sortingOrder = 12;
            ufo.transform.localScale = Vector3.one * 3.30f;

            yield return new WaitForSeconds(UfoSpawnPauseSeconds);
            if (ufo == null || !bossActive || gameOver) yield break;
            yield return StartCoroutine(AcceleratingMove(ufo.transform, hover, UfoApproachDashDuration));

            if (target != null && !target.IsDefeated)
            {
                var direction = ((Vector2)target.transform.position - (Vector2)ufo.transform.position).normalized;
                if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
                ufo.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
                StartCoroutine(FireBossSunflowerSeed((Vector2)ufo.transform.position, target, direction));
            }

            // The missile flies independently; the hamster immediately turns around and exits.
            yield return StartCoroutine(SmoothTurn(ufo.transform, ufo.transform.eulerAngles.z, ufo.transform.eulerAngles.z + 180f, 0.30f));
            yield return StartCoroutine(SmoothMove(ufo.transform, outside, 0.70f));
            Destroy(ufo);
        }

        private IEnumerator FireBossSunflowerSeed(Vector2 origin, TileMonster target, Vector2 direction)
        {
            var missile = new GameObject("Boss Sunflower Seed Bomb");
            missile.transform.position = new Vector3(origin.x, origin.y, -1f);
            var renderer = missile.AddComponent<SpriteRenderer>();
            renderer.sprite = sunflowerSeedSprite;
            renderer.sortingOrder = 13;
            missile.transform.localScale = Vector3.one * 0.96f;
            missile.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));

            // Keep the pre-existing seed behaviour: wait, then accelerate sharply.
            yield return new WaitForSeconds(SunflowerMissilePauseSeconds);
            if (missile == null) yield break;

            yield return StartCoroutine(HomeBossSunflowerSeed(missile.transform, target, direction, 0.52f));
            if (target != null && !target.IsDefeated) target.TakeDamage(BossSunflowerDamage);
            // A boss seed is consumed the instant it reaches its monster target.
            if (missile != null) Destroy(missile);
        }

        // Steer toward the target's current position each frame so the visual
        // seed actually meets a moving monster before its damage is applied.
        private static IEnumerator HomeBossSunflowerSeed(Transform missile, TileMonster target, Vector2 fallbackDirection, float duration)
        {
            var origin = (Vector2)missile.position;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (missile == null) yield break;
                var destination = target != null && !target.IsDefeated
                    ? (Vector2)target.transform.position
                    : origin + fallbackDirection * 5f;
                var direction = destination - (Vector2)missile.position;
                if (direction.sqrMagnitude > 0.001f)
                    missile.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
                var progress = Mathf.Clamp01(elapsed / duration);
                missile.position = Vector2.Lerp(origin, destination, progress * progress * progress);
                yield return null;
            }
            if (missile != null && target != null && !target.IsDefeated)
                missile.position = target.transform.position;
        }

        private Vector2 GetBossArenaExit(Vector2 origin, Vector2 direction)
        {
            var bounds = BossArenaBounds;
            var xDistance = direction.x > 0.001f ? (bounds.xMax - origin.x) / direction.x :
                direction.x < -0.001f ? (bounds.xMin - origin.x) / direction.x : float.PositiveInfinity;
            var yDistance = direction.y > 0.001f ? (bounds.yMax - origin.y) / direction.y :
                direction.y < -0.001f ? (bounds.yMin - origin.y) / direction.y : float.PositiveInfinity;
            var travel = Mathf.Min(xDistance > 0f ? xDistance : float.PositiveInfinity,
                yDistance > 0f ? yDistance : float.PositiveInfinity);
            if (float.IsInfinity(travel)) travel = 8f;
            return origin + direction * (travel + 0.4f);
        }

        public void PlayMonsterHitEffect(Vector2 position)
        {
            StartCoroutine(PlaySunflowerExplosion(position));
        }

        public void ReportMonsterDestroyed(TileMonster monster)
        {
            if (monster == null) return;
            tileMonsters.Remove(monster);
            if (!bossActive || gameOver) return;
            score += BossKillScore;
            if (BossEnemiesRemaining > 0) return;

            // Freeze the visible clock and stop its coroutine at the exact clear moment.
            bossCountdownFrozenRemaining = BossTimeRemaining;
            bossCountdownFrozen = true;
            if (bossCountdownRoutine != null)
            {
                StopCoroutine(bossCountdownRoutine);
                bossCountdownRoutine = null;
            }
            var elapsed = Mathf.Clamp(BossDurationSeconds - bossCountdownFrozenRemaining, 0f, BossDurationSeconds);
            // <=2 seconds = 28%, <=4 = 26%, ... <=30 = 0% of the 12,000 kill reward.
            var twoSecondStep = Mathf.Clamp(Mathf.CeilToInt(elapsed / 2f), 1, 15);
            var bonusPercent = Mathf.Max(0, 30 - twoSecondStep * 2);
            score += Mathf.RoundToInt(BossMonsterCount * BossKillScore * bonusPercent / 100f);
            if (!bossClearSequenceRunning) StartCoroutine(PlayBossClearSequence());
        }

        // Clear celebration: the hamster descends from above, creates the
        // rainbow seed at the centre, then turns around before GAME CLEAR appears.
        private IEnumerator PlayBossClearSequence()
        {
            bossClearSequenceRunning = true;
            var bounds = BossArenaBounds;
            var outside = new Vector2(0f, bounds.yMax + 2f);
            var centre = Vector2.zero;
            var ufo = new GameObject("Boss Clear Hamster UFO");
            ufo.transform.position = new Vector3(outside.x, outside.y, -2f);
            // 원본 UFO는 아래를 향한 상태가 0도입니다. 내려올 때는 그대로 보이고,
            // 돌아갈 때만 뒤집어서 위쪽으로 향하게 합니다.
            ufo.transform.rotation = Quaternion.identity;
            var ufoRenderer = ufo.AddComponent<SpriteRenderer>();
            ufoRenderer.sprite = ufoEventSprite;
            ufoRenderer.sortingOrder = 30;
            ufo.transform.localScale = Vector3.one * 3.30f;

            yield return new WaitForSeconds(UfoSpawnPauseSeconds);
            yield return StartCoroutine(AcceleratingMove(ufo.transform, centre, UfoApproachDashDuration));

            var reward = new GameObject("Victory Rainbow Sunflower Seed");
            reward.transform.position = new Vector3(centre.x, centre.y, -3f);
            reward.transform.localScale = Vector3.one * 0.08f;
            var seedCreatedAt = Time.time;
            var rewardRenderer = reward.AddComponent<SpriteRenderer>();
            rewardRenderer.sprite = victoryRainbowSunflowerSeedSprite != null ? victoryRainbowSunflowerSeedSprite : sunflowerSeedSprite;
            rewardRenderer.sortingOrder = 31;
            GameObject rewardHalo = null;
            if (victoryRainbowHaloSprite != null)
            {
                rewardHalo = new GameObject("Victory Rainbow Sunlight Halo");
                rewardHalo.transform.position = new Vector3(centre.x, centre.y, -3.1f);
                var haloRenderer = rewardHalo.AddComponent<SpriteRenderer>();
                haloRenderer.sprite = victoryRainbowHaloSprite;
                haloRenderer.sortingOrder = 30; // Keep the rainbow seed in front of the light ring.
                rewardHalo.AddComponent<VictoryRainbowHalo>().Configure(haloRenderer, VictoryRainbowSeedHaloScale);
            }
            for (var elapsed = 0f; elapsed < 0.32f; elapsed += Time.deltaTime)
            {
                reward.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.65f, elapsed / 0.32f);
                yield return null;
            }
            reward.transform.localScale = Vector3.one * 0.65f;

            yield return StartCoroutine(SmoothTurn(ufo.transform, 0f, 180f, 0.30f));
            yield return StartCoroutine(SmoothMove(ufo.transform, outside, 0.70f));
            Destroy(ufo);
            var remainingSeedTime = Mathf.Max(0f, BossClearSeedDisplaySeconds - (Time.time - seedCreatedAt));
            yield return new WaitForSeconds(remainingSeedTime);
            if (rewardHalo != null) Destroy(rewardHalo);
            Destroy(reward);
            EndGame(true);
        }

        public void Merge(Ball first, Ball second)
        {
            // Saturn is the final normal planet.  Two Saturns remain as separate planets.
            if (gameOver || first == null || second == null || first.Tier != second.Tier || first.Tier >= 6) return;
            var nextTier = first.Tier + 1;
            var firstBody = first.GetComponent<Rigidbody2D>();
            var secondBody = second.GetComponent<Rigidbody2D>();
            var position = ((Vector2)first.transform.position + (Vector2)second.transform.position) * 0.5f;
            var carriedVelocity = ((firstBody != null ? firstBody.velocity : Vector2.zero) + (secondBody != null ? secondBody.velocity : Vector2.zero)) * 0.5f;
            var mergeDirection = ((Vector2)first.transform.position - (Vector2)second.transform.position).normalized;
            if (mergeDirection.sqrMagnitude < 0.01f) mergeDirection = Vector2.up;
            // A merge keeps most of its motion. Stationary merges receive a subtle nudge instead of freezing.
            if (carriedVelocity.magnitude < 0.55f) carriedVelocity = mergeDirection * 0.7f;
            else carriedVelocity *= 0.82f;
            first.gameObject.SetActive(false);
            second.gameObject.SetActive(false);
            BowlingAudio.PlayMerge();
            AddScore(first.Tier);
            BowlingCamera.Kick(0.12f);
            if (nextTier < BlackHoleTier)
            {
                var merged = CreateBall(nextTier, position, true);
                if (merged == null) return;
                merged.IsLaunchedLineage = first.IsLaunchedLineage || second.IsLaunchedLineage;
                // The merged planet owns the midpoint.  Move overlapping neighbours
                // outward instead of relocating the newly created, larger planet.
                merged.PushOverlappingPlanetsAway();
                merged.SetMergeCooldown(0.18f);
                merged.GetComponent<Rigidbody2D>().velocity = Vector2.ClampMagnitude(carriedVelocity, 8f);
            }
        }

        private void CreateCamera()
        {
            var cameraObject = new GameObject("Bowling Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            // 화면 비율이 맞지 않아 남는 카메라 영역은 게임 확장이 아니라 검은 여백으로 처리합니다.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<BowlingCamera>();
        }

        private void CreateLane()
        {
            if (dirtBackgroundSprite != null)
            {
                var camera = Camera.main;
                var visibleWidth = camera.orthographicSize * 2f * camera.aspect;
                // Sprite.Create uses the texture width as one world unit. Account for the
                // actual image ratio so both portrait and landscape backgrounds fill the camera.
                var requiredWorldHeight = camera.orthographicSize * 2f + 0.5f;
                var yScale = requiredWorldHeight * dirtBackgroundSprite.texture.width / dirtBackgroundSprite.texture.height;
                var dirt = CreateVisual("Firefly Background", Vector2.zero, new Vector2(visibleWidth + 0.5f, yScale), Color.white, -5);
                dirt.GetComponent<SpriteRenderer>().sprite = dirtBackgroundSprite;
            }
            CreateVisual("Lane Background", new Vector2(0f, 0.05f), new Vector2(9.6f, 12.15f), new Color(0.02f, 0.06f, 0.16f, 0.45f), -1);
            CreateVisual("Lane", new Vector2(0f, 0.05f), new Vector2(8.7f, 11.65f), new Color(0.08f, 0.20f, 0.42f, 0.24f), 0);
            // Infinite mode deliberately has no left, top, or right rail tiles.
            // The bottom out line remains, so its selected HP still controls game over.
            if (!GameDifficultySettings.InfiniteMode)
            {
                CreateSideRailTiles("Left", -4.55f, leftWallTiles);
                CreateSideRailTiles("Right", 4.55f, rightWallTiles);
                if (!SideTilesAndUfoEnabled)
                {
                    foreach (var tile in leftWallTiles) tile.SetActive(false);
                    foreach (var tile in rightWallTiles) tile.SetActive(false);
                }
                CreateTopRailTiles();
            }
            // Invisible sensor aligned to the lower edge of the playable field.
            CreateBox("Bottom Out Line", new Vector2(0f, -5.95f), new Vector2(9.1f, 0.11f), new Color(1f, 0.18f, 0.18f, 0f), 3).isTrigger = true;

            for (var i = -3; i <= 3; i++)
                CreateVisual("Lane Marker", new Vector2(i * 1.2f, 0.05f), new Vector2(0.035f, 11.15f), new Color(0.9f, 0.72f, 0.42f, 0.22f), 1);
        }

        private void CreateSideRailTiles(string sideName, float xPosition, List<GameObject> tiles)
        {
            const float railHeight = 12.15f;
            var tileHeight = railHeight / WallTileCount;
            var bottom = 0.05f - railHeight * 0.5f;
            for (var i = 0; i < WallTileCount; i++)
            {
                var tile = CreateRail($"{sideName} Wall Tile {i + 1}", new Vector2(xPosition, bottom + tileHeight * (i + 0.5f)), new Vector2(0.55f, tileHeight));
                // Adjacent physics tiles overlap slightly. This removes sub-pixel cracks that
                // can let a fast large planet slip between two visible wall tiles.
                tile.size = new Vector2(0.70f, tileHeight + 0.08f);
                tile.gameObject.AddComponent<Wall>();
                tiles.Add(tile.gameObject);
            }
        }

        private void CreateTopRailTiles()
        {
            const float railWidth = 9.1f;
            var tileWidth = railWidth / WallTileCount;
            var left = -railWidth * 0.5f;
            for (var i = 0; i < WallTileCount; i++)
            {
                var tile = CreateRail($"Top Wall Tile {i + 1}", new Vector2(left + tileWidth * (i + 0.5f), 6.10f), new Vector2(tileWidth, 0.55f));
                tile.size = new Vector2(tileWidth + 0.08f, 0.70f);
                tile.gameObject.AddComponent<Wall>();
                topWallTiles.Add(tile.gameObject);
            }
        }

        public bool CanExitThroughBrokenSide(bool leftSide, float yPosition, float radius)
        {
            return brokenSideGaps.Any(gap => gap.LeftSide == leftSide &&
                Mathf.Abs(yPosition - gap.CenterY) <= gap.Height * 0.5f - radius);
        }

        private IEnumerator RunUfoTileEvents()
        {
            ufoEventRunning = true;
            while (pendingUfoEvents > 0 && !gameOver)
            {
                pendingUfoEvents--;
                var canBreakLeft = leftWallTiles.Any(tile => tile != null);
                var canBreakRight = rightWallTiles.Any(tile => tile != null);
                if (!canBreakLeft && !canBreakRight)
                {
                    pendingUfoEvents = 0;
                    break;
                }

                var breakLeft = canBreakLeft && (!canBreakRight || Random.value < 0.5f);
                var candidates = breakLeft ? leftWallTiles : rightWallTiles;
                var remaining = candidates.Where(tile => tile != null).ToList();
                var targetTile = remaining[Random.Range(0, remaining.Count)];
                yield return StartCoroutine(PlayUfoTileBreak(breakLeft, targetTile));
                yield return new WaitForSeconds(0.65f);
            }
            ufoEventRunning = false;
        }

        private IEnumerator PlayUfoTileBreak(bool breakLeft, GameObject targetTile)
        {
            if (targetTile == null) yield break;
            var target = (Vector2)targetTile.transform.position;
            // Enter from beyond the camera edge, then pause in the centre of the free HUD space.
            var outsideX = breakLeft ? -12.2f : 12.2f;
            var hoverX = breakLeft ? -8.35f : 8.35f;
            // The source art's forward direction is opposite to the previous assumption.
            // Left-wall attacks must visibly face right, and right-wall attacks face left.
            var facingAngle = breakLeft ? 90f : -90f;

            var ufo = new GameObject("Hamster UFO Tile Event");
            ufo.transform.position = new Vector3(outsideX, target.y, -2f);
            ufo.transform.rotation = Quaternion.Euler(0f, 0f, facingAngle);
            var ufoRenderer = ufo.AddComponent<SpriteRenderer>();
            ufoRenderer.sprite = ufoEventSprite;
            ufoRenderer.sortingOrder = 12;
            ufo.transform.localScale = Vector3.one * 3.30f;

            // Briefly show the UFO at the edge, then make it accelerate into its firing position.
            yield return new WaitForSeconds(UfoSpawnPauseSeconds);
            yield return StartCoroutine(AcceleratingMove(ufo.transform, new Vector2(hoverX, target.y), UfoApproachDashDuration));

            var missile = new GameObject("Sunflower Seed Missile");
            missile.transform.position = ufo.transform.position;
            var missileRenderer = missile.AddComponent<SpriteRenderer>();
            missileRenderer.sprite = sunflowerSeedSprite;
            missileRenderer.sortingOrder = 13;
            missile.transform.localScale = Vector3.one * 0.96f;
            var missileDirection = (target - (Vector2)missile.transform.position).normalized;
            missile.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, missileDirection));
            // The missile continues on its own; the UFO must not wait for its impact.
            var missileFlight = StartCoroutine(FireSunflowerSeed(missile, target, targetTile, breakLeft));
            yield return StartCoroutine(SmoothTurn(ufo.transform, facingAngle, facingAngle + 180f, 0.42f));
            yield return StartCoroutine(SmoothMove(ufo.transform, new Vector2(outsideX, target.y), 0.75f));
            Destroy(ufo);
            yield return missileFlight;
        }

        private IEnumerator FireSunflowerSeed(GameObject missile, Vector2 target, GameObject targetTile, bool breakLeft)
        {
            yield return new WaitForSeconds(SunflowerMissilePauseSeconds);
            if (missile != null) yield return StartCoroutine(AcceleratingMove(missile.transform, target, 0.64f));
            if (missile != null) Destroy(missile);
            if (targetTile == null) yield break;

            var explosion = StartCoroutine(PlaySunflowerExplosion(target));

            // Destroying the tile removes both its sprite and BoxCollider2D, leaving a real gap.
            brokenSideGaps.Add(new WallGap { LeftSide = breakLeft, CenterY = target.y, Height = 12.15f / WallTileCount });
            var tileRenderer = targetTile.GetComponent<SpriteRenderer>();
            if (tileRenderer != null) tileRenderer.enabled = false;
            var tileCollider = targetTile.GetComponent<Collider2D>();
            if (tileCollider != null) tileCollider.enabled = false;
            BowlingAudio.PlayBomb();
            Destroy(targetTile);
            yield return explosion;
        }

        private IEnumerator PlaySunflowerExplosion(Vector2 position)
        {
            if (sunflowerExplosionSprite == null) yield break;
            var explosion = new GameObject("Sunflower Seed Explosion");
            explosion.transform.position = new Vector3(position.x, position.y, -3f);
            explosion.transform.localScale = Vector3.one * 0.28f;
            var renderer = explosion.AddComponent<SpriteRenderer>();
            renderer.sprite = sunflowerExplosionSprite;
            renderer.sortingOrder = 20;

            for (var elapsed = 0f; elapsed < SunflowerExplosionDuration; elapsed += Time.deltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / SunflowerExplosionDuration);
                explosion.transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.35f, progress);
                renderer.color = new Color(1f, 1f, 1f, 1f - progress);
                yield return null;
            }
            Destroy(explosion);
        }

        private static IEnumerator SmoothMove(Transform item, Vector2 destination, float duration)
        {
            var origin = (Vector2)item.position;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                item.position = Vector2.Lerp(origin, destination, t);
                yield return null;
            }
            item.position = destination;
        }

        // Cubic easing starts almost still, then rapidly accelerates into the target.
        private static IEnumerator AcceleratingMove(Transform item, Vector2 destination, float duration)
        {
            var origin = (Vector2)item.position;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                item.position = Vector2.Lerp(origin, destination, t * t * t);
                yield return null;
            }
            item.position = destination;
        }

        private static IEnumerator SmoothTurn(Transform item, float fromAngle, float toAngle, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                item.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            item.rotation = Quaternion.Euler(0f, 0f, toAngle);
        }

        private struct WallGap
        {
            public bool LeftSide;
            public float CenterY;
            public float Height;
        }

        private void CreateAimLine()
        {
            var lineObject = new GameObject("Slingshot Aim Arrow");
            aimLine = lineObject.AddComponent<LineRenderer>();
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.startColor = new Color(1f, 0.94f, 0.3f);
            aimLine.endColor = new Color(1f, 0.4f, 0.12f);
            aimLine.startWidth = 0.08f;
            aimLine.endWidth = 0.02f;
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }

        #if false // Removed score events: rat holes and frozen-field speed boost.
        private void TriggerRatEventIfDue()
        {
            while (score >= nextRatEventScore)
            {
                if (nextRatEventScore == 3000)
                {
                    var sides = new List<RailSide> { RailSide.Left, RailSide.Top, RailSide.Right };
                    for (var i = sides.Count - 1; i > 0; i--)
                    {
                        var swap = Random.Range(0, i + 1);
                        var temporary = sides[i]; sides[i] = sides[swap]; sides[swap] = temporary;
                    }
                    CreateRatHole(sides[0]);
                    CreateRatHole(sides[1]);
                }
                else CreateRatHole((RailSide)Random.Range(0, 3));

                nextRatEventScore += 6000;
            }
        }

        private void TriggerFreezeEventIfDue()
        {
            if (fieldFrozen && score >= freezeEndsAtScore)
            {
                fieldFrozen = false;
                if (frostOverlay != null) Destroy(frostOverlay);
            }
            while (score >= nextFreezeEventScore)
            {
                fieldFrozen = true;
                freezeEndsAtScore = nextFreezeEventScore + 3000;
                nextFreezeEventScore += 6000;
                if (frostOverlay == null)
                    frostOverlay = CreateVisual("Frozen Field Effect", new Vector2(0f, 0.05f), new Vector2(8.68f, 11.62f), new Color(0.38f, 0.82f, 1f, 0.28f), 1);
                foreach (var ball in Object.FindObjectsOfType<Ball>()) ball.DoubleSpeed();
            }
        }

        private void CreateRatHole(RailSide side)
        {
            // Orange ball diameter is 0.825 world units; a small margin makes the hole playable.
            const float holeSize = 0.96f;
            var center = side == RailSide.Top ? Random.Range(-2.9f, 2.9f) : Random.Range(-2.2f, 4.2f);
            railHoles.Add(new RailHole { Side = side, Center = center, Size = holeSize });

            var originalRail = side == RailSide.Left ? leftRail : side == RailSide.Right ? rightRail : topRail;
            originalRail.enabled = false;
            RebuildPhysicalRailSegments(side);

            var holePosition = side == RailSide.Left ? new Vector2(-4.55f, center) : side == RailSide.Right ? new Vector2(4.55f, center) : new Vector2(center, 6.10f);
            var holeVisualSize = side == RailSide.Top ? new Vector2(holeSize, 0.60f) : new Vector2(0.60f, holeSize);
            CreateVisual("Rat Wall Hole", holePosition, holeVisualSize, new Color(0.035f, 0.022f, 0.012f), 3);
            CreateRat3D(holePosition, side);
        }

        private void RebuildPhysicalRailSegments(RailSide side)
        {
            var axisMin = side == RailSide.Top ? -4.55f : -6.025f;
            var axisMax = side == RailSide.Top ? 4.55f : 6.125f;
            foreach (var segment in ratEventRailSegments)
                if (segment != null && segment.name == "Rat Event Rail Segment " + side) Destroy(segment);
            ratEventRailSegments.RemoveAll(segment => segment == null || segment.name == "Rat Event Rail Segment " + side);

            var holes = railHoles.FindAll(hole => hole.Side == side);
            holes.Sort((a, b) => a.Center.CompareTo(b.Center));
            var cursor = axisMin;
            foreach (var hole in holes)
            {
                var gapMin = Mathf.Clamp(hole.Center - hole.Size * 0.5f, axisMin, axisMax);
                var gapMax = Mathf.Clamp(hole.Center + hole.Size * 0.5f, axisMin, axisMax);
                CreateRailSegment(side, cursor, gapMin);
                cursor = Mathf.Max(cursor, gapMax);
            }
            CreateRailSegment(side, cursor, axisMax);
        }

        private void CreateRailSegment(RailSide side, float start, float end)
        {
            if (end - start < 0.03f) return;
            var segment = new GameObject("Rat Event Rail Segment " + side);
            var collider = segment.AddComponent<BoxCollider2D>();
            collider.size = side == RailSide.Top ? new Vector2(end - start, 0.55f) : new Vector2(0.55f, end - start);
            collider.offset = Vector2.zero;
            segment.transform.position = side == RailSide.Left ? new Vector2(-4.55f, (start + end) * 0.5f) : side == RailSide.Right ? new Vector2(4.55f, (start + end) * 0.5f) : new Vector2((start + end) * 0.5f, 6.10f);
            segment.AddComponent<Wall>();
            ratEventRailSegments.Add(segment);
        }

        private void CreateRat3D(Vector2 holePosition, RailSide side)
        {
            var rat = new GameObject("3D Rat Event");
            rat.transform.position = new Vector3(holePosition.x, holePosition.y, -1.2f);
            var direction = side == RailSide.Left ? 1f : side == RailSide.Right ? -1f : 0f;
            CreateRatPart(rat.transform, "Body", new Vector3(-direction * 0.16f, -0.02f, 0f), new Vector3(0.42f, 0.25f, 0.26f), new Color(0.38f, 0.34f, 0.30f));
            CreateRatPart(rat.transform, "Head", new Vector3(direction * 0.18f, 0.02f, -0.05f), new Vector3(0.25f, 0.22f, 0.22f), new Color(0.48f, 0.44f, 0.39f));
            CreateRatPart(rat.transform, "Ear A", new Vector3(direction * 0.15f, 0.17f, -0.08f), new Vector3(0.12f, 0.12f, 0.08f), new Color(0.94f, 0.52f, 0.55f));
            CreateRatPart(rat.transform, "Ear B", new Vector3(direction * 0.27f, 0.13f, -0.08f), new Vector3(0.10f, 0.10f, 0.08f), new Color(0.94f, 0.52f, 0.55f));
            if (side == RailSide.Top) rat.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        }

        private static void CreateRatPart(Transform parent, string partName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = partName;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var renderer = part.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color")) { color = color };
        }

        public bool IsHoleOpen(RailSide side, float coordinate, float radius)
        {
            foreach (var hole in railHoles)
                if (hole.Side == side && Mathf.Abs(coordinate - hole.Center) + radius <= hole.Size * 0.5f)
                    return true;
            return false;
        }

        #endif

        private void SpawnPreview()
        {
            if (followingTier < 0)
            {
                selectedTier = RollSpawnTier();
                followingTier = RollSpawnTier();
                afterFollowingTier = RollSpawnTier();
            }
            else
            {
                selectedTier = followingTier;
                followingTier = afterFollowingTier;
                afterFollowingTier = RollSpawnTier();
            }
            previewBall = CreateBall(selectedTier, launcher, false);
            previewBall.name = "Next Bowling Ball";
        }

        private int RollSpawnTier()
        {
            var roll = Random.value;
            if (bossActive)
            {
                // 50,000-point tile battle: all seven merge planets can be launched.
                // Tiers 0-4: 12% each, tiers 5-6: 10% each,
                // black hole/lightning: 10% each (100% total).
                if (roll < 0.12f) return 0;
                if (roll < 0.24f) return 1;
                if (roll < 0.36f) return 2;
                if (roll < 0.48f) return 3;
                if (roll < 0.60f) return 4;
                if (roll < 0.70f) return 5;
                if (roll < 0.80f) return 6;
                if (roll < 0.90f) return BlackHoleTier;
                return LightningTier;
            }
            // Normal play: Sprite 0 50%, Sprite 1 30%, Sprite 2 16%,
            // black hole 2%, and lightning 2%.
            if (roll < 0.50f) return 0;
            if (roll < 0.80f) return 1;
            if (roll < 0.96f) return 2;
            if (roll < 0.98f) return BlackHoleTier;
            return LightningTier;
        }

        private Ball CreateBall(int tier, Vector2 position, bool launched)
        {
            var prefab = planetPrefabs != null && tier >= 0 && tier < planetPrefabs.Length ? planetPrefabs[tier] : null;
            var ballObject = prefab != null
                ? Instantiate(prefab, position, Quaternion.identity)
                : new GameObject($"{BallNames[tier]} Ball");
            ballObject.name = $"{BallNames[tier]} Ball";
            ballObject.transform.position = position;
            var renderer = ballObject.GetComponent<SpriteRenderer>() ?? ballObject.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = GetBallSprite(tier);
            renderer.sortingOrder = 4;
            if (renderer.sprite == circleSprite && glossyMarbleSprite != null && tier != BlackHoleTier)
            {
                // Neutral glass rim: it sits above the coloured ball, without adding a new colour border.
                var gloss = new GameObject("Glass Rim");
                gloss.transform.SetParent(ballObject.transform);
                gloss.transform.localPosition = Vector3.zero;
                gloss.transform.localScale = Vector3.one;
                var glossRenderer = gloss.AddComponent<SpriteRenderer>();
                glossRenderer.sprite = glossyMarbleSprite;
                glossRenderer.color = new Color(1f, 1f, 1f, 0.38f);
                glossRenderer.sortingOrder = 5;
            }
            var ball = ballObject.GetComponent<Ball>() ?? ballObject.AddComponent<Ball>();
            ball.SetTier(tier);
            // The next-ball preview is only a picture.  A prefab collider must not become
            // an invisible obstacle before that ball has actually been launched.
            if (!launched)
            {
                foreach (var collider in ballObject.GetComponentsInChildren<Collider2D>())
                    collider.enabled = false;
                var previewBody = ballObject.GetComponent<Rigidbody2D>();
                if (previewBody != null) previewBody.simulated = false;
            }
            if (launched)
            {
                // Prefabs intentionally contain only artwork and editable colliders.  Add
                // the runtime physics body explicitly after instantiation; this avoids a
                // missing Rigidbody2D when a prefab has been edited in the Inspector.
                var body = ballObject.GetComponent<Rigidbody2D>();
                if (body == null) body = ballObject.AddComponent<Rigidbody2D>();
                if (body == null)
                {
                    Debug.LogError($"Could not add Rigidbody2D to {ballObject.name}.");
                    Destroy(ballObject);
                    return null;
                }
                body.simulated = true;
                body.gravityScale = 0f;
                // A black hole travels with the same slowing behaviour as a normal ball.
                body.drag = 1.1f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var railCollider = ballObject.GetComponents<PolygonCollider2D>()
                    .FirstOrDefault(collider => !collider.isTrigger);
                if (railCollider == null)
                {
                    railCollider = ballObject.AddComponent<PolygonCollider2D>();
                    railCollider.points = GetBallColliderOutline(tier);
                    railCollider.isTrigger = false;
                }
                if (tier == BlackHoleTier)
                {
                    // A black hole's trigger collider is no longer created or used for
                    // automatic absorption. Its only action is the Q-key ring burst.
                    ball.ConfigureBlackHoleRailCollider(railCollider);
                }
                ball.Activate();
            }
            return ball;
        }

        private void UpdateAim(Vector2 mouse)
        {
            var pull = Vector2.ClampMagnitude((launcher - mouse) * PullSensitivity, MaxLaunchPull);
            aimLine.enabled = true;
            aimLine.SetPosition(0, launcher);
            aimLine.SetPosition(1, launcher + pull);
            if (previewBall != null) previewBall.transform.position = launcher - pull * 0.25f;
        }

        private void Launch(Vector2 mouse)
        {
            dragging = false;
            aimLine.enabled = false;
            var pull = Vector2.ClampMagnitude((launcher - mouse) * PullSensitivity, MaxLaunchPull);
            if (pull.magnitude < 0.15f) return;
            var tier = selectedTier;
            Destroy(previewBall.gameObject);
            previewBall = null;
            var ball = CreateBall(tier, launcher, true);
            ball.IsLaunchedLineage = true;
            var launchMultiplier = tier == BlackHoleTier ? BlackHoleLaunchSpeedMultiplier : 1f;
            ball.GetComponent<Rigidbody2D>().AddForce(pull * NormalLaunchImpulse * launchMultiplier, ForceMode2D.Impulse);
            BowlingAudio.PlayLaunch();
            // Keep a tiny, deliberate reload beat before showing the next ball.
            nextThrowTime = Time.time + ReloadDelaySeconds;
            StartCoroutine(ReloadPreviewAfterDelay());
        }

        private IEnumerator ReloadPreviewAfterDelay()
        {
            yield return new WaitForSeconds(ReloadDelaySeconds);
            if (!gameOver && previewBall == null) SpawnPreview();
        }

        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private IEnumerator RestartRound()
        {
            gameOver = false;
            dragging = false;
            aimLine.enabled = false;
            score = StartingScore;
            maxLives = GameDifficultySettings.StartingLives;
            lives = maxLives;
            followingTier = -1;
            afterFollowingTier = -1;
            foreach (var ball in Object.FindObjectsOfType<Ball>()) Destroy(ball.gameObject);
            yield return null;
            nextThrowTime = Time.time + 0.1f;
            SpawnPreview();
        }

        private BoxCollider2D CreateBox(string objectName, Vector2 position, Vector2 size, Color color, int order)
        {
            var item = CreateVisual(objectName, position, size, color, order);
            return item.AddComponent<BoxCollider2D>();
        }

        private BoxCollider2D CreateRail(string objectName, Vector2 position, Vector2 size)
        {
            var item = CreateVisual(objectName, position, size, Color.white, 2);
            var renderer = item.GetComponent<SpriteRenderer>();
            if (spaceStationWallSprite != null)
            {
                renderer.sprite = spaceStationWallSprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = size;
                item.transform.localScale = Vector3.one;
            }
            else renderer.color = new Color(0.25f, 0.12f, 0.055f);
            var collider = item.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        private GameObject CreateVisual(string objectName, Vector2 position, Vector2 size, Color color, int order)
        {
            var item = new GameObject(objectName);
            item.transform.position = position;
            item.transform.localScale = size;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return item;
        }

        private static void CreateRuntimeSprites()
        {
            if (circleSprite != null) return;
            const int size = 64;
            var circle = new Texture2D(size, size, TextureFormat.RGBA32, false);
            circle.filterMode = FilterMode.Point;
            var square = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var dx = x - (size - 1) * 0.5f;
                var dy = y - (size - 1) * 0.5f;
                circle.SetPixel(x, y, dx * dx + dy * dy <= 31f * 31f ? Color.white : Color.clear);
            }
            circle.Apply();
            square.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            square.Apply();
            circleSprite = Sprite.Create(circle, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            squareSprite = Sprite.Create(square, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var marbleTexture = Resources.Load<Texture2D>("Balls/GlossyMarble");
            if (marbleTexture != null)
                glossyMarbleSprite = Sprite.Create(marbleTexture, new Rect(0, 0, marbleTexture.width, marbleTexture.height), Vector2.one * 0.5f, marbleTexture.width);
            var planetTexture = Resources.Load<Texture2D>("Balls/PlanetSprites");
            if (planetTexture != null)
            {
                planetTexture.filterMode = FilterMode.Point;
                var cellWidth = planetTexture.width / 4f;
                var cellHeight = planetTexture.height / 2f;
                planetSprites = new Sprite[8];
                // Texture coordinates start at the bottom-left: bottom row is Earth through Black Hole.
                planetSprites[0] = CreatePlanetSprite(planetTexture, 0, 1, cellWidth, cellHeight);
                planetSprites[1] = CreatePlanetSprite(planetTexture, 1, 1, cellWidth, cellHeight);
                planetSprites[2] = CreatePlanetSprite(planetTexture, 2, 1, cellWidth, cellHeight);
                // PlanetSprites_3(Mars)와 PlanetSprites_4(Earth)의 게임 순서를 교환.
                planetSprites[3] = CreatePlanetSprite(planetTexture, 0, 0, cellWidth, cellHeight);
                planetSprites[4] = CreatePlanetSprite(planetTexture, 3, 1, cellWidth, cellHeight);
                planetSprites[5] = CreatePlanetSprite(planetTexture, 1, 0, cellWidth, cellHeight);
                planetSprites[6] = CreatePlanetSprite(planetTexture, 2, 0, cellWidth, cellHeight);
                planetSprites[7] = CreatePlanetSprite(planetTexture, 3, 0, cellWidth, cellHeight);
                planetColliderOutlines = new Vector2[planetSprites.Length][];
                for (var i = 0; i < planetSprites.Length; i++)
                    planetColliderOutlines[i] = CreateAlphaOutline(planetSprites[i], 48);
            }
            planetPrefabs = new GameObject[LightningTier + 1];
            for (var tier = 0; tier <= BlackHoleTier; tier++)
                planetPrefabs[tier] = Resources.Load<GameObject>($"PlanetPrefabs/PlanetSprites_{PlanetPrefabOrder[tier]}");
            planetPrefabs[LightningTier] = Resources.Load<GameObject>("PlanetPrefabs/LightningBeamBall");
            if (planetPrefabs[LightningTier] != null)
                lightningBallSprite = planetPrefabs[LightningTier].GetComponent<SpriteRenderer>()?.sprite;
            lightningChainBeamSprites = new Sprite[3];
            var lightningBeamNames = new[] { "LightningChainBeam_A", "LightningChainBeam_B", "LightningChainBeam_C" };
            for (var i = 0; i < lightningBeamNames.Length; i++)
            {
                var beamTexture = Resources.Load<Texture2D>($"Balls/{lightningBeamNames[i]}");
                if (beamTexture == null) continue;
                beamTexture.filterMode = FilterMode.Point;
                lightningChainBeamSprites[i] = Sprite.Create(beamTexture,
                    new Rect(0, 0, beamTexture.width, beamTexture.height), Vector2.one * 0.5f, beamTexture.height);
            }
            var backgroundTexture = Resources.Load<Texture2D>("Backgrounds/Background");
            if (backgroundTexture != null)
                dirtBackgroundSprite = Sprite.Create(backgroundTexture, new Rect(0, 0, backgroundTexture.width, backgroundTexture.height), Vector2.one * 0.5f, backgroundTexture.width);
            var wallTexture = Resources.Load<Texture2D>("SpaceStation/SpaceStationWallTile");
            if (wallTexture != null)
                spaceStationWallSprite = Sprite.Create(wallTexture, new Rect(0, 0, wallTexture.width, wallTexture.height), Vector2.one * 0.5f, wallTexture.width);
            var ufoTexture = Resources.Load<Texture2D>("SpaceStation/UfoHamsterEventTransparent");
            if (ufoTexture != null)
                ufoEventSprite = Sprite.Create(ufoTexture, new Rect(0, 0, ufoTexture.width, ufoTexture.height), Vector2.one * 0.5f, ufoTexture.width);
            var sunflowerSeedTexture = Resources.Load<Texture2D>("SpaceStation/SunflowerSeedMissileSelected");
            if (sunflowerSeedTexture != null)
                sunflowerSeedSprite = Sprite.Create(sunflowerSeedTexture, new Rect(0, 0, sunflowerSeedTexture.width, sunflowerSeedTexture.height), Vector2.one * 0.5f, sunflowerSeedTexture.width);
            var victorySeedTexture = Resources.Load<Texture2D>("SpaceStation/VictoryRainbowSunflowerSeed");
            if (victorySeedTexture != null)
                victoryRainbowSunflowerSeedSprite = Sprite.Create(victorySeedTexture, new Rect(0, 0, victorySeedTexture.width, victorySeedTexture.height), Vector2.one * 0.5f, victorySeedTexture.width);
            var victoryHaloTexture = Resources.Load<Texture2D>("SpaceStation/VictoryRainbowSunlightBurstV6");
            if (victoryHaloTexture != null)
            {
                victoryHaloTexture.filterMode = FilterMode.Point;
                victoryRainbowHaloSprite = Sprite.Create(victoryHaloTexture,
                    new Rect(0, 0, victoryHaloTexture.width, victoryHaloTexture.height), Vector2.one * 0.5f, victoryHaloTexture.width);
            }
            var explosionTexture = Resources.Load<Texture2D>("SpaceStation/SunflowerExplosion");
            if (explosionTexture != null)
                sunflowerExplosionSprite = Sprite.Create(explosionTexture, new Rect(0, 0, explosionTexture.width, explosionTexture.height), Vector2.one * 0.5f, explosionTexture.width);
            var blackHoleRingTexture = Resources.Load<Texture2D>("SpaceStation/BlackHoleRing");
            if (blackHoleRingTexture != null)
            {
                blackHoleRingTexture.filterMode = FilterMode.Point;
                blackHoleRingSprite = Sprite.Create(blackHoleRingTexture, new Rect(0, 0, blackHoleRingTexture.width, blackHoleRingTexture.height), Vector2.one * 0.5f, blackHoleRingTexture.width);
            }
            var topTileMonsterTexture = Resources.Load<Texture2D>("SpaceStation/TopTileCrabEnemy");
            if (topTileMonsterTexture != null)
            {
                topTileMonsterTexture.filterMode = FilterMode.Point;
                topTileMonsterSprite = Sprite.Create(topTileMonsterTexture,
                    new Rect(0, 0, topTileMonsterTexture.width, topTileMonsterTexture.height), Vector2.one * 0.5f, topTileMonsterTexture.width);
            }
            var topTileWalkATexture = Resources.Load<Texture2D>("SpaceStation/TopTileCrabWalkA");
            var topTileWalkBTexture = Resources.Load<Texture2D>("SpaceStation/TopTileCrabWalkB");
            if (topTileWalkATexture != null && topTileWalkBTexture != null)
            {
                topTileWalkATexture.filterMode = FilterMode.Point;
                topTileWalkBTexture.filterMode = FilterMode.Point;
                topTileMonsterWalkSprites = new[]
                {
                    Sprite.Create(topTileWalkATexture, new Rect(0, 0, topTileWalkATexture.width, topTileWalkATexture.height), Vector2.one * 0.5f, topTileWalkATexture.width),
                    Sprite.Create(topTileWalkBTexture, new Rect(0, 0, topTileWalkBTexture.width, topTileWalkBTexture.height), Vector2.one * 0.5f, topTileWalkBTexture.width)
                };
            }
        }

        private static Sprite CreatePlanetSprite(Texture2D texture, int column, int row, float cellWidth, float cellHeight)
        {
            return Sprite.Create(texture, new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight), Vector2.one * 0.5f, cellWidth);
        }

        // Converts the actual opaque pixel edge of a sprite to local physics points.
        // This removes the mismatch caused by transparent padding in a sprite sheet.
        private static Vector2[] CreateAlphaOutline(Sprite sprite, int segments)
        {
            try
            {
                var texture = sprite.texture;
                var rect = sprite.textureRect;
                var center = rect.center;
                var maximumRadius = Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) * 0.5f - 1f);
                var points = new Vector2[segments];
                for (var i = 0; i < segments; i++)
                {
                    var angle = i * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var lastOpaqueRadius = 0;
                    for (var radius = 1; radius <= maximumRadius; radius++)
                    {
                        var x = Mathf.Clamp(Mathf.RoundToInt(center.x + direction.x * radius), 0, texture.width - 1);
                        var y = Mathf.Clamp(Mathf.RoundToInt(center.y + direction.y * radius), 0, texture.height - 1);
                        if (texture.GetPixel(x, y).a > 0.08f) lastOpaqueRadius = radius;
                    }
                    points[i] = direction * (lastOpaqueRadius / rect.width);
                }
                return points;
            }
            catch (UnityException)
            {
                return CreateCircularOutline(0.5f, segments);
            }
        }

        private static Vector2[] CreateCircularOutline(float radius, int segments)
        {
            var points = new Vector2[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static Vector2[] CreateSaturnOutline()
        {
            // Clockwise exterior only: it is deliberately wider than tall to include the
            // two ring tips while keeping the collider inside the transparent padding.
            return new[]
            {
                new Vector2(-0.53f, -0.10f), new Vector2(-0.43f, -0.26f),
                new Vector2(-0.22f, -0.40f), new Vector2(0.05f, -0.43f),
                new Vector2(0.32f, -0.33f), new Vector2(0.53f, -0.15f),
                new Vector2(0.57f, 0.03f), new Vector2(0.43f, 0.20f),
                new Vector2(0.20f, 0.34f), new Vector2(-0.08f, 0.41f),
                new Vector2(-0.33f, 0.30f), new Vector2(-0.55f, 0.13f)
            };
        }
    }

    public sealed class Wall : MonoBehaviour
    {
        private void Awake() => GetComponent<BoxCollider2D>().sharedMaterial = new PhysicsMaterial2D("Bouncy Rail") { bounciness = 0.88f, friction = 0.05f };
    }

    // WAV files live in Assets/Resources/Audio so they also load in a WebGL build.
        public sealed class BowlingAudio : MonoBehaviour
        {
            private static BowlingAudio instance;
            // Individual multiplier for each chain-link lightning sound.
            private const float LightningVolume = 3f;
            private const float MonsterImpactVolume = 0.85f;
            private const float MonsterImpactCooldown = 0.16f;
            // Individual multiplier for the final lightning-chain explosion only.
            private const float LightningExplosionVolume = 1.5f;
            // Individual multiplier for the Q-key black-hole ability sound.
            private const float BlackHoleAbilityVolume = 4f;
        private AudioSource effects;
        private AudioSource music;
        private AudioClip launchClip;
        private AudioClip bombClip;
        private AudioClip mergeClip;
        private AudioClip coinClip;
        private AudioClip lightningClip;
        private AudioClip lightningExplosionClip;
        private AudioClip blackHoleAbilityClip;
        private AudioClip bossWarningClip;
        private float nextMonsterImpactSoundTime;

        private void Awake()
        {
            if (instance != null) { Destroy(gameObject); return; }
            instance = this;
            effects = gameObject.AddComponent<AudioSource>();
            launchClip = Resources.Load<AudioClip>("Audio/LaunchSound");
            bombClip = Resources.Load<AudioClip>("Audio/BombSound");
            mergeClip = Resources.Load<AudioClip>("Audio/MergeSound04");
            coinClip = Resources.Load<AudioClip>("Audio/CoinSound02");
            lightningClip = Resources.Load<AudioClip>("Audio/Lightning");
            lightningExplosionClip = Resources.Load<AudioClip>("Audio/LightningExplosion");
            blackHoleAbilityClip = Resources.Load<AudioClip>("Audio/BlackHoleAbility");
            bossWarningClip = Resources.Load<AudioClip>("Audio/BossWarning");

            // If HomeScene already started the BGM, keep that same source playing through the transition.
            if (!HomeBackgroundMusic.IsActive)
            {
                music = gameObject.AddComponent<AudioSource>();
                music.clip = Resources.Load<AudioClip>("Audio/Background_FreeJump");
                music.loop = true;
            }
            ApplyVolumes();
            if (music != null && music.clip != null) music.Play();
        }

        public static void ApplyVolumes()
        {
            if (instance == null) return;
            if (instance.effects != null) instance.effects.volume = 0.48f * SoundSettings.SfxVolume;
            if (instance.music != null) instance.music.volume = 0.11f * SoundSettings.BgmVolume;
        }

        public static void PlayLaunch()
        {
            if (instance != null && instance.launchClip != null) instance.effects.PlayOneShot(instance.launchClip);
        }

        public static void PlayBomb()
        {
            if (instance != null && instance.bombClip != null) instance.effects.PlayOneShot(instance.bombClip);
        }

        // Used for tile-crab damage only. Closely timed hits share one sound.
        public static void PlayMonsterImpact()
        {
            if (instance == null || instance.bombClip == null || Time.time < instance.nextMonsterImpactSoundTime) return;
            instance.nextMonsterImpactSoundTime = Time.time + MonsterImpactCooldown;
            instance.effects.PlayOneShot(instance.bombClip, MonsterImpactVolume);
        }

        public static void PlayMerge()
        {
            if (instance != null && instance.mergeClip != null) instance.effects.PlayOneShot(instance.mergeClip);
        }

        public static void PlayCoin()
        {
            if (instance != null && instance.coinClip != null) instance.effects.PlayOneShot(instance.coinClip);
        }

        public static void PlayLightning()
        {
            if (instance != null && instance.lightningClip != null)
                instance.effects.PlayOneShot(instance.lightningClip, LightningVolume);
        }

        public static void PlayLightningExplosion()
        {
            if (instance != null && instance.lightningExplosionClip != null)
                instance.effects.PlayOneShot(instance.lightningExplosionClip, LightningExplosionVolume);
        }

        public static void PlayBlackHoleAbility()
        {
            if (instance != null && instance.blackHoleAbilityClip != null)
                instance.effects.PlayOneShot(instance.blackHoleAbilityClip, BlackHoleAbilityVolume);
        }

        public static void PlayBossWarning()
        {
            if (instance != null && instance.bossWarningClip != null)
                instance.effects.PlayOneShot(instance.bossWarningClip);
        }
    }

    // HomeScene has no game-audio object, so it owns a small dedicated BGM source.
    public sealed class HomeBackgroundMusic : MonoBehaviour
    {
        private static HomeBackgroundMusic instance;
        private AudioSource source;
        public static bool IsActive => instance != null;

        private void Awake()
        {
            if (instance != null) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>("Audio/Background_FreeJump");
            source.loop = true;
            ApplyVolume();
            if (source.clip != null) source.Play();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void ApplyVolume()
        {
            if (instance != null && instance.source != null)
                instance.source.volume = 0.11f * SoundSettings.BgmVolume;
        }
    }

    public sealed class SoundSettingsUI : MonoBehaviour
    {
        // Change these two constants to resize the settings button and popup.
        private const float GearSize = 112f;
        private const float PopupWidth = 556f;
        private const float PopupHeight = 408f;
        private static SoundSettingsUI instance;
        private bool open;
        private Texture2D gearTexture;
        private GUIStyle label;
        private GUIStyle panel;
        private GUIStyle button;
        private Texture2D opaquePanelBackground;
        private Texture2D buttonNormalBackground;
        private Texture2D buttonHoverBackground;
        private Texture2D buttonActiveBackground;
        private Texture2D sliderTrackBackground;
        private Texture2D sliderThumbBackground;
        private Texture2D sliderThumbHoverBackground;
        private GUIStyle sliderStyle;
        private GUIStyle sliderThumbStyle;

        private void Awake()
        {
            instance = this;
            gearTexture = Resources.Load<Texture2D>("UI/SettingsGear");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            // Game-over Esc has priority: BowlingSuikaGame sends the player Home instead.
            var game = Object.FindFirstObjectByType<BowlingSuikaGame>();
            if (game != null && game.GameOver) return;
            open = !open;
        }

        private static Rect GetGearRect() => new Rect(Screen.width - GearSize - 22f, 18f, GearSize, GearSize);
        private static Rect GetPopupRect() => new Rect(Screen.width - PopupWidth - 22f, GearSize + 30f, PopupWidth, PopupHeight);

        public static bool IsPointerOverInteractiveUI()
        {
            if (instance == null) return false;
            var pointer = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return GetGearRect().Contains(pointer) || (instance.open && GetPopupRect().Contains(pointer));
        }

        private void OnGUI()
        {
            // HomeScene's UI is drawn by a separate OnGUI component.  A lower GUI
            // depth is rendered in front, so keep both the gear and this popup on top.
            var previousDepth = GUI.depth;
            GUI.depth = -1000;

            label ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 34, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            if (panel == null)
            {
                opaquePanelBackground = new Texture2D(1, 1);
                opaquePanelBackground.SetPixel(0, 0, new Color(0.17f, 0.18f, 0.21f, 0.98f));
                opaquePanelBackground.Apply();
                buttonNormalBackground = CreateSolidTexture(new Color(0.10f, 0.11f, 0.15f, 0.98f));
                buttonHoverBackground = CreateSolidTexture(new Color(0.15f, 0.30f, 0.50f, 1f));
                buttonActiveBackground = CreateSolidTexture(new Color(0.08f, 0.48f, 0.72f, 1f));
                // WebGL does not reliably include the editor's default slider skin.
                // Supply a tiny runtime texture for both parts so the track and handle
                // are always drawn in a browser build.
                sliderTrackBackground = CreateSolidTexture(new Color(0.05f, 0.07f, 0.11f, 1f));
                sliderThumbBackground = CreateSolidTexture(new Color(0.20f, 0.75f, 1f, 1f));
                sliderThumbHoverBackground = CreateSolidTexture(new Color(0.58f, 0.91f, 1f, 1f));
                panel = new GUIStyle(GUI.skin.box) { font = BowlingSuikaGame.PixelFont, fontSize = 34, alignment = TextAnchor.UpperCenter };
                panel.normal.background = opaquePanelBackground;
                panel.normal.textColor = Color.white;
                button = new GUIStyle(GUI.skin.button)
                {
                    font = BowlingSuikaGame.PixelFont,
                    fontSize = 26,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white, background = buttonNormalBackground },
                    hover = { textColor = Color.white, background = buttonHoverBackground },
                    active = { textColor = Color.white, background = buttonActiveBackground },
                    focused = { textColor = Color.white, background = buttonNormalBackground }
                };
                sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
                {
                    fixedHeight = 14f,
                    normal = { background = sliderTrackBackground },
                    hover = { background = sliderTrackBackground },
                    active = { background = sliderTrackBackground },
                    focused = { background = sliderTrackBackground }
                };
                sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
                {
                    fixedWidth = 28f,
                    fixedHeight = 28f,
                    normal = { background = sliderThumbBackground },
                    hover = { background = sliderThumbHoverBackground },
                    active = { background = sliderThumbHoverBackground },
                    focused = { background = sliderThumbBackground }
                };
            }
            var gearRect = GetGearRect();
            if (gearTexture != null)
            {
                if (GUI.Button(gearRect, gearTexture, GUIStyle.none)) open = !open;
            }
            else if (GUI.Button(gearRect, "설정")) open = !open;
            if (!open)
            {
                GUI.depth = previousDepth;
                return;
            }

            var popup = GetPopupRect();
            GUI.Box(popup, "SOUND", panel);
            GUI.Label(new Rect(popup.x + 36, popup.y + 90, 124, 52), "BGM", label);
            GUI.Label(new Rect(popup.x + 36, popup.y + 196, 124, 52), "SFX", label);
            var previousBgm = SoundSettings.BgmVolume;
            var previousSfx = SoundSettings.SfxVolume;
            SoundSettings.BgmVolume = GUI.HorizontalSlider(new Rect(popup.x + 172, popup.y + 102, 330, 40), SoundSettings.BgmVolume, 0f, 1f, sliderStyle, sliderThumbStyle);
            SoundSettings.SfxVolume = GUI.HorizontalSlider(new Rect(popup.x + 172, popup.y + 208, 330, 40), SoundSettings.SfxVolume, 0f, 1f, sliderStyle, sliderThumbStyle);
            GUI.Label(new Rect(popup.x + 172, popup.y + 138, 330, 40), $"{Mathf.RoundToInt(SoundSettings.BgmVolume * 100f)}%", label);
            GUI.Label(new Rect(popup.x + 172, popup.y + 244, 330, 40), $"{Mathf.RoundToInt(SoundSettings.SfxVolume * 100f)}%", label);
            if (!Mathf.Approximately(previousBgm, SoundSettings.BgmVolume) || !Mathf.Approximately(previousSfx, SoundSettings.SfxVolume)) SoundSettings.Apply();
            if (GUI.Button(new Rect(popup.x + 154, popup.y + 300, 248, 62), "메인화면으로", button))
            {
                open = false;
                SceneManager.LoadScene("HomeScene");
            }
            GUI.depth = previousDepth;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }

    public sealed class Ball : MonoBehaviour
    {
        // Change this table to tune all ball diameters. 1 unit is the red ball's base multiplier.
        private static readonly float[] SizeMultipliers = { 15f, 23f, 30f, 50f, 70f, 90f, 120f, 23f, 23f };
        private const float BaseBallScale = 0.01f;
        public int Tier { get; private set; }
        private BowlingSuikaGame game;
        private bool active;
        private bool merging;
        private float mergeCooldownUntil;
        private bool enteredField;
        private Rigidbody2D body;
        private const float BottomFieldOutY = -5.95f;
        private const float FieldEntryY = -4.9f;
        private float activatedAt;
        private float stoppedAt = -1f;
        private bool fieldExitHandled;
        private bool blackHoleRingUsed;
        private bool lightningChainClaimed;
        private Vector2 velocityBeforePhysicsStep;
        private float angularVelocityBeforePhysicsStep;
        // The black hole's non-trigger collider is reserved for the rails. Any trigger
        // stored in its prefab is intentionally inert until the player presses S.
        private Collider2D blackHoleRailCollider;
        public bool IsLaunchedLineage { get; set; }
        private float localColliderExtent = 0.5f;
        // These are the inside faces of the unbroken side/top/bottom rails.  They are
        // used only while moving a neighbouring planet out of a merge overlap; normal
        // movement still respects destroyed-tile gaps.
        private const float MergeRailInsideX = 4.22f;
        private const float MergeRailBottomY = -5.72f;
        private const float MergeRailTopY = 5.82f;
        // The ring expands the black hole radius by half of its original radius.
        private const float BlackHoleRingOuterRadiusMultiplier = 1.5f;
        private const float BlackHoleRingDuration = 0.5f;
        // Q ability is active: black hole keeps drifting at this very slow world speed.
        private const float BlackHoleRingMoveSpeed = 0.45f;
        // Physics contacts can be missed when dense pixel-art polygon colliders slide
        // along each other.  This small allowance lets visually touching planets merge.
        private const float SameTierMergeProximityMultiplier = 1.08f;
        private const float SameTierMergeProximityPadding = 0.035f;
        // A lightning ball must never remain in play forever when it misses every planet.
        private const float LightningAutomaticDeleteDelay = 4f;
        // The generated ring artwork occupies about 92% of its square canvas.
        private const float BlackHoleRingArtworkCoverage = 0.92f;

        public bool CanActivateBlackHoleRing => active && Tier == BowlingSuikaGame.BlackHoleTier &&
                                                 !blackHoleRingUsed && body != null;
        public bool IsAvailableForLightningChain => active && !lightningChainClaimed;
        private float WorldColliderExtent => localColliderExtent * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        public bool TryClaimForLightningChain()
        {
            if (!IsAvailableForLightningChain) return false;
            lightningChainClaimed = true;
            // Claimed planets must not merge while the expanding scan is travelling.
            SetMergeCooldown(8f);
            var visual = GetComponent<LightningChargeVisual>() ?? gameObject.AddComponent<LightningChargeVisual>();
            visual.Configure(GetComponent<SpriteRenderer>());
            return true;
        }

        public void SetTier(int tier)
        {
            Tier = tier;
            var rootRenderer = GetComponent<SpriteRenderer>();
            // A prefab's Sprite and PolygonCollider2D share the same coordinate system.
            // Keep that Sprite when it exists; replacing it with the old generated grid
            // sprite made the visible planet smaller/larger than its physics outline.
            if (rootRenderer.sprite == null)
            {
                var sprite = BowlingSuikaGame.GetBallSprite(tier);
                rootRenderer.sprite = sprite;
                rootRenderer.color = sprite == BowlingSuikaGame.CircleSprite
                    ? BowlingSuikaGame.BallColors[tier]
                    : Color.white;
            }

            var scale = BaseBallScale * SizeMultipliers[tier];
            if (tier == BowlingSuikaGame.LightningTier && rootRenderer.sprite != null)
            {
                // The lightning PNG has a different Pixels Per Unit from PlanetSprites.
                // Match its real world diameter to the black-hole prefab, independent of
                // whatever Transform Scale is saved in the editable prefab.
                var lightningDiameter = Mathf.Max(rootRenderer.sprite.bounds.size.x, rootRenderer.sprite.bounds.size.y);
                var blackHoleDiameter = BowlingSuikaGame.GetPrefabSpriteDiameter(BowlingSuikaGame.BlackHoleTier);
                if (lightningDiameter > 0.0001f) scale *= blackHoleDiameter / lightningDiameter;
            }
            transform.localScale = Vector3.one * scale;

            localColliderExtent = GetColliderExtent();
        }

        private float GetColliderExtent()
        {
            var polygon = GetComponent<PolygonCollider2D>();
            if (polygon != null && polygon.pathCount > 0)
            {
                var extent = 0.1f;
                for (var pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
                {
                    foreach (var point in polygon.GetPath(pathIndex))
                        extent = Mathf.Max(extent, point.magnitude);
                }
                return extent;
            }

            return BowlingSuikaGame.GetBallColliderExtent(Tier);
        }

        public void Activate()
        {
            game = Object.FindFirstObjectByType<BowlingSuikaGame>();
            active = true;
            body = GetComponent<Rigidbody2D>();
            activatedAt = Time.time;
            IgnoreBlackHoleBallContacts();
        }

        public void SetMergeCooldown(float seconds)
        {
            mergeCooldownUntil = Time.time + Mathf.Max(0f, seconds);
        }

        public void PushOverlappingPlanetsAway()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null) return;

            // A large merged planet keeps its midpoint.  Several short passes let a
            // tightly packed chain of surrounding planets move outward without any
            // single physics impulse ejecting a planet through a wall tile.
            for (var pass = 0; pass < 4; pass++)
            {
                var neighbours = Object.FindObjectsByType<Ball>(FindObjectsSortMode.None);
                foreach (var neighbour in neighbours)
                {
                    if (neighbour == this || !neighbour.active || neighbour.merging ||
                        neighbour.Tier == BowlingSuikaGame.BlackHoleTier) continue;

                    var offset = (Vector2)neighbour.transform.position - (Vector2)transform.position;
                    var distance = offset.magnitude;
                    var requiredDistance = WorldColliderExtent + neighbour.WorldColliderExtent + 0.035f;
                    if (distance >= requiredDistance) continue;

                    // Coincident centers are uncommon, but choose a deterministic
                    // direction so the neighbouring ball can still be separated.
                    var direction = distance > 0.001f
                        ? offset / distance
                        : ((neighbour.GetInstanceID() & 1) == 0 ? Vector2.right : Vector2.left);
                    neighbour.MoveOutsideMergeRadius((Vector2)transform.position + direction * requiredDistance);
                }
            }
        }

        private void MoveOutsideMergeRadius(Vector2 requestedPosition)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null) return;
            var extent = WorldColliderExtent + 0.035f;
            var maxX = Mathf.Max(0f, MergeRailInsideX - extent);
            var minY = MergeRailBottomY + extent;
            var maxY = MergeRailTopY - extent;
            var safePosition = new Vector2(
                Mathf.Clamp(requestedPosition.x, -maxX, maxX),
                Mathf.Clamp(requestedPosition.y, minY, Mathf.Max(minY, maxY)));
            // Teleporting this neighbour to a non-overlapping point and clearing its
            // velocity prevents it from receiving the large collision bounce that
            // previously pushed balls through wall tiles.
            var position = safePosition;
            body.position = position;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }

        public void ConfigureBlackHoleRailCollider(Collider2D collider)
        {
            blackHoleRailCollider = collider;
        }

        // Keep the black hole physically solid against rails, while ignoring planet impacts.
        // The black hole does not delete anything simply by touching it.
        private void IgnoreBlackHoleBallContacts()
        {
            var allBalls = Object.FindObjectsByType<Ball>(FindObjectsSortMode.None);
            if (Tier == BowlingSuikaGame.BlackHoleTier && blackHoleRailCollider != null)
            {
                foreach (var other in allBalls)
                {
                    if (other == this) continue;
                    foreach (var otherCollider in other.GetComponents<Collider2D>())
                        Physics2D.IgnoreCollision(blackHoleRailCollider, otherCollider, true);
                }
                return;
            }

            foreach (var blackHole in allBalls)
            {
                if (blackHole == this || blackHole.Tier != BowlingSuikaGame.BlackHoleTier ||
                    blackHole.blackHoleRailCollider == null) continue;
                foreach (var ownCollider in GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(blackHole.blackHoleRailCollider, ownCollider, true);
            }
        }

        private void FixedUpdate()
        {
            if (active && body != null)
            {
                velocityBeforePhysicsStep = body.velocity;
                angularVelocityBeforePhysicsStep = body.angularVelocity;
                TryMergeNearbySameTier();
            }
            KeepInsideRails();
        }

        private void TryMergeNearbySameTier()
        {
            if (game == null || merging || Time.time < mergeCooldownUntil ||
                Tier >= BowlingSuikaGame.BlackHoleTier) return;

            foreach (var other in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                if (other == this || !other.active || other.merging || other.Tier != Tier) continue;

                var allowedDistance = (WorldColliderExtent + other.WorldColliderExtent) * SameTierMergeProximityMultiplier
                                      + SameTierMergeProximityPadding;
                if (((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude > allowedDistance * allowedDistance)
                    continue;

                // Claim both planets before merging so one FixedUpdate cannot trigger
                // two merges for the same pair.
                merging = true;
                other.merging = true;
                game.Merge(this, other);
                return;
            }
        }

        private void RestoreVelocityBeforePhysicsStep()
        {
            if (body == null) return;
            body.velocity = velocityBeforePhysicsStep;
            body.angularVelocity = angularVelocityBeforePhysicsStep;
        }

        // The physical tile colliders are the only rail authority.  Do not add an invisible
        // safety boundary here: a removed tile must always be a passable hole.
        public void KeepInsideRails()
        {
            if (!active || body == null) return;
        }

        private void Update()
        {
            if (!active) return;
            if (game.GameOver)
            {
                return;
            }
            var isSpecialBall = Tier == BowlingSuikaGame.BlackHoleTier || Tier == BowlingSuikaGame.LightningTier;
            var blackHoleRingIsActive = Tier == BowlingSuikaGame.BlackHoleTier && blackHoleRingUsed;
            if (Tier == BowlingSuikaGame.LightningTier && Time.time - activatedAt >= LightningAutomaticDeleteDelay)
            {
                Destroy(gameObject);
                return;
            }
            if (isSpecialBall && !blackHoleRingIsActive && Time.time - activatedAt > 0.35f)
            {
                if (body.velocity.sqrMagnitude < 0.01f)
                {
                    if (stoppedAt < 0f) stoppedAt = Time.time;
                    else if (Time.time - stoppedAt > 0.45f) Destroy(gameObject);
                }
                else stoppedAt = -1f;
            }
            if (isSpecialBall)
            {
                // Ability balls leave by themselves instead of consuming a life.
                var outsideSpecialArea = game.BossActive
                    ? IsOutsideBossArena()
                    : transform.position.x < -4.7f || transform.position.x > 4.7f || transform.position.y < BottomFieldOutY || transform.position.y > 6.35f;
                if (!blackHoleRingIsActive && outsideSpecialArea)
                    Destroy(gameObject);
                return;
            }
            // During the tile battle, ordinary planets can travel across the whole
            // background. They simply disappear only after leaving the camera view.
            if (game.BossActive)
            {
                if (IsOutsideBossArena()) Destroy(gameObject);
                return;
            }
            if (transform.position.y > FieldEntryY) enteredField = true;
            if (!enteredField)
            {
                if (transform.position.y < BottomFieldOutY) LeaveField();
                return;
            }
            if (enteredField && (transform.position.y < BottomFieldOutY || transform.position.y > 6.35f ||
                transform.position.x < -4.9f || transform.position.x > 4.9f))
                LeaveField();
        }

        private bool IsOutsideBossArena()
        {
            var bounds = game.BossArenaBounds;
            var position = (Vector2)transform.position;
            return position.x < bounds.xMin || position.x > bounds.xMax ||
                   position.y < bounds.yMin || position.y > bounds.yMax;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!active || merging || Time.time < mergeCooldownUntil) return;
            if (Tier == BowlingSuikaGame.LightningTier)
            {
                // The lightning ball starts a same-tier chain from the first ball it
                // hits, then disappears. Rails do not trigger this effect.
                var struckMonster = collision.collider.GetComponentInParent<TileMonster>();
                if (struckMonster != null)
                {
                    RestoreVelocityBeforePhysicsStep();
                    game.TriggerLightningMonsterChain(struckMonster);
                    Destroy(gameObject);
                    return;
                }
                var struckBall = collision.collider.GetComponentInParent<Ball>();
                if (struckBall != null && struckBall != this && struckBall.active)
                {
                    // Undo the contact impulse immediately, so lightning can trigger a
                    // chain without physically knocking the struck planet away.
                    RestoreVelocityBeforePhysicsStep();
                    struckBall.RestoreVelocityBeforePhysicsStep();
                    game.TriggerLightningChain(struckBall);
                    Destroy(gameObject);
                }
                return;
            }
            if (Tier == BowlingSuikaGame.BlackHoleTier)
            {
                var collidedMonster = collision.collider.GetComponentInParent<TileMonster>();
                if (collidedMonster != null)
                {
                    // Monsters must not bounce the ability ball away before Q can place
                    // the ring over them.  The ring itself performs the instant kill.
                    if (blackHoleRailCollider != null) Physics2D.IgnoreCollision(blackHoleRailCollider, collision.collider, true);
                    RestoreVelocityBeforePhysicsStep();
                    return;
                }
                // Planets pass through the black hole. Only an actual rail reflects it.
                if (collision.collider.GetComponentInParent<Ball>() == null && velocityBeforePhysicsStep.sqrMagnitude > 0.0001f)
                {
                    // Rails still reflect the ball, but never reduce its speed.
                    var reflected = Vector2.Reflect(velocityBeforePhysicsStep, collision.GetContact(0).normal);
                    body.velocity = reflected.normalized * velocityBeforePhysicsStep.magnitude;
                }
                return;
            }
            var monster = collision.collider.GetComponentInParent<TileMonster>();
            if (monster != null)
            {
                // Each ordinary planet is a one-use projectile in the boss phase.
                // Tier damage follows the merge sequence: 1, 2, 4, 8, 16, 32, 64.
                monster.TakeDamage(1 << Mathf.Clamp(Tier, 0, 6));
                Destroy(gameObject);
                return;
            }
            if (!collision.collider.TryGetComponent<Ball>(out var other) || other.merging) return;
            if (other.Tier == BowlingSuikaGame.BlackHoleTier) return;
            if (other.Tier != Tier)
            {
                return;
            }
            // The largest Saturn is a stable final planet and must never merge away.
            if (Tier >= 6) return;
            merging = true;
            other.merging = true;
            game.Merge(this, other);
        }

        public void ActivateBlackHoleRing()
        {
            if (!CanActivateBlackHoleRing) return;
            blackHoleRingUsed = true;
            // Q keeps the ability moving, but at a deliberately very slow speed.
            // Preserve its current travel direction; an already-stopped hole drifts upward.
            var direction = body.velocity.sqrMagnitude > 0.0001f
                ? body.velocity.normalized
                : velocityBeforePhysicsStep.sqrMagnitude > 0.0001f ? velocityBeforePhysicsStep.normalized : Vector2.up;
            body.velocity = direction * BlackHoleRingMoveSpeed;
            body.angularVelocity = 0f;
            StartCoroutine(BlackHoleRingBurst());
        }

        private IEnumerator BlackHoleRingBurst()
        {
            var coreRadius = WorldColliderExtent;
            var outerRadius = coreRadius * BlackHoleRingOuterRadiusMultiplier;

            var effect = new GameObject("Black Hole Ring");
            var effectRenderer = effect.AddComponent<SpriteRenderer>();
            effectRenderer.sprite = BowlingSuikaGame.BlackHoleRingSprite;
            effectRenderer.sortingOrder = 7;
            effectRenderer.color = Color.white;
            // The complete ring appears on the same frame as the S-key press.
            var visibleDiameter = outerRadius * 2f / BlackHoleRingArtworkCoverage;
            effect.transform.localScale = Vector3.one * visibleDiameter;
            effect.transform.position = transform.position;
            RemoveBallsInsideRing(outerRadius);
            RemoveMonstersInsideRing(outerRadius);

            for (var elapsed = 0f; elapsed < BlackHoleRingDuration; elapsed += Time.deltaTime)
            {
                if (this == null) break;
                effect.transform.position = transform.position;
                // Keep clearing balls that enter the already-visible ring before it expires.
                RemoveBallsInsideRing(outerRadius);
                RemoveMonstersInsideRing(outerRadius);
                yield return null;
            }
            Destroy(effect);
            if (this != null) Destroy(gameObject);
        }

        private void RemoveBallsInsideRing(float outerRadius)
        {
            foreach (var other in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                // Black holes are the ability's source; they never remove themselves or another black hole.
                if (other == null || other == this || other.Tier == BowlingSuikaGame.BlackHoleTier || !other.active) continue;
                var distance = Vector2.Distance(transform.position, other.transform.position);
                if (distance > outerRadius + other.WorldColliderExtent) continue;
                Destroy(other.gameObject);
            }
        }

        private void RemoveMonstersInsideRing(float outerRadius)
        {
            foreach (var monster in Object.FindObjectsByType<TileMonster>(FindObjectsSortMode.None))
            {
                if (monster == null || monster.IsDefeated) continue;
                if (Vector2.Distance(transform.position, monster.transform.position) <= outerRadius + monster.WorldRadius)
                    monster.DestroyImmediately();
            }
        }

        private void LeaveField()
        {
            if (fieldExitHandled) return;
            fieldExitHandled = true;
            game.LoseLife(this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (game != null && game.BossActive) return;
            if (active && Tier != BowlingSuikaGame.BlackHoleTier && Tier != BowlingSuikaGame.LightningTier &&
                enteredField && other.name == "Bottom Out Line") LeaveField();
        }
    }

    // One independently moving top-tile creature.  Its world bounds deliberately
    // match the visible lane, so it keeps bouncing even after the top rail is removed.
    public sealed class TileMonster : MonoBehaviour
    {
        private BowlingSuikaGame game;
        private Rigidbody2D body;
        private int hitPoints;
        private bool defeated;
        private Vector2 velocityBeforeCollision;
        private float moveSpeed;
        private SpriteRenderer eyeGlow;
        private SpriteRenderer bodyRenderer;
        // Raise these values to make the top-tile monsters even faster.
        private const float MinimumMoveSpeed = 6.5f;
        private const float MaximumMoveSpeed = 9.0f;
        // Two-frame walking animation. Smaller values make the legs switch faster.
        private const float WalkFrameSeconds = 0.18f;

        // Damage states: 64~33 HP = full speed, 32~17 HP = half speed,
        // and 16 HP or less = one quarter of the original speed.
        private float CurrentMoveSpeed => hitPoints <= 16 ? moveSpeed * 0.25f :
                                          hitPoints <= 32 ? moveSpeed * 0.5f : moveSpeed;

        public bool IsDefeated => defeated;
        public float WorldRadius => 0.42f;

        public void Configure(BowlingSuikaGame owner, int startingHitPoints)
        {
            game = owner;
            hitPoints = startingHitPoints;
            body = GetComponent<Rigidbody2D>();
            bodyRenderer = GetComponent<SpriteRenderer>();
            var direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            moveSpeed = Random.Range(MinimumMoveSpeed, MaximumMoveSpeed);
            body.velocity = direction * moveSpeed;
            CreateEyeGlow();
            UpdateEyeGlow();
        }

        private void FixedUpdate()
        {
            if (defeated || body == null) return;
            UpdateWalkAnimation();
            velocityBeforeCollision = body.velocity;
            var bounds = game != null ? game.BossArenaBounds : Rect.MinMaxRect(-10f, -6f, 10f, 6f);
            var position = body.position;
            var velocity = body.velocity;
            if (velocity.sqrMagnitude < 0.0001f) velocity = Random.insideUnitCircle.normalized;
            velocity = velocity.normalized * CurrentMoveSpeed;
            if (position.x <= bounds.xMin && velocity.x < 0f || position.x >= bounds.xMax && velocity.x > 0f) velocity.x = -velocity.x;
            if (position.y <= bounds.yMin && velocity.y < 0f || position.y >= bounds.yMax && velocity.y > 0f) velocity.y = -velocity.y;
            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            body.position = position;
            body.velocity = velocity;
        }

        private void UpdateWalkAnimation()
        {
            if (bodyRenderer == null) return;
            var frame = Mathf.FloorToInt(Time.time / WalkFrameSeconds) % 2;
            // Use frame B as the master leg pose. The alternate frame is its
            // horizontal mirror, making a clean two-step left/right walk cycle.
            var walkSprite = BowlingSuikaGame.GetTopTileMonsterWalkSprite(1);
            if (walkSprite != null)
            {
                bodyRenderer.sprite = walkSprite;
                bodyRenderer.flipX = frame == 0;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (defeated || body == null) return;
            if (collision.collider.GetComponentInParent<Ball>() != null)
            {
                // A planet deals damage, but it never pushes a tile monster away.
                body.velocity = velocityBeforeCollision.sqrMagnitude > 0.0001f
                    ? velocityBeforeCollision.normalized * CurrentMoveSpeed
                    : Random.insideUnitCircle.normalized * CurrentMoveSpeed;
                return;
            }
            if (collision.contactCount > 0)
                body.velocity = Vector2.Reflect(body.velocity.normalized, collision.GetContact(0).normal) * CurrentMoveSpeed;
        }

        public void TakeDamage(int damage)
        {
            if (defeated || damage <= 0) return;
            // Every successful monster hit, including a planet collision and a
            // boss hamster seed, uses the same impact sound and sunflower burst.
            BowlingAudio.PlayMonsterImpact();
            game?.PlayMonsterHitEffect(transform.position);
            hitPoints -= damage;
            UpdateEyeGlow();
            if (hitPoints <= 0) DestroyImmediately();
            else if (body != null && body.velocity.sqrMagnitude > 0.0001f)
                body.velocity = body.velocity.normalized * CurrentMoveSpeed;
        }

        private void CreateEyeGlow()
        {
            var eye = new GameObject("Monster Eye Health Glow");
            eye.transform.SetParent(transform, false);
            // The source eye is centred in the artwork; this small additive-looking
            // overlay changes only its light while leaving the body sprite intact.
            eye.transform.localPosition = new Vector3(0f, -0.015f, -0.1f);
            eye.transform.localScale = Vector3.one * 0.12f;
            eyeGlow = eye.AddComponent<SpriteRenderer>();
            eyeGlow.sprite = BowlingSuikaGame.CircleSprite;
            eyeGlow.sortingOrder = 4;
        }

        private void UpdateEyeGlow()
        {
            if (eyeGlow == null) return;
            if (hitPoints <= 16) eyeGlow.color = new Color(1f, 0.10f, 0.06f, 0.72f);
            else if (hitPoints <= 32) eyeGlow.color = new Color(1f, 0.84f, 0.05f, 0.68f);
            else eyeGlow.color = new Color(0.08f, 0.72f, 1f, 0.60f);
        }

        public void DestroyImmediately()
        {
            if (defeated) return;
            defeated = true;
            game?.ReportMonsterDestroyed(this);
            Destroy(gameObject);
        }
    }

    // Follows both moving targets until the lightning chain resolves, continuously
    // stretching and rotating the artwork so no visual gap appears between them.
    public sealed class LightningChainBeam : MonoBehaviour
    {
        private Transform first;
        private Transform second;
        private SpriteRenderer beamRenderer;
        private float height;

        public void Configure(Transform firstTarget, Transform secondTarget, SpriteRenderer renderer, float beamHeight)
        {
            first = firstTarget;
            second = secondTarget;
            beamRenderer = renderer;
            height = beamHeight;
            UpdateBeam();
        }

        private void LateUpdate() => UpdateBeam();

        private void UpdateBeam()
        {
            if (first == null || second == null || beamRenderer == null)
            {
                Destroy(gameObject);
                return;
            }

            var start = (Vector2)first.transform.position;
            var end = (Vector2)second.transform.position;
            var direction = end - start;
            var distance = direction.magnitude;
            if (distance < 0.001f) return;

            transform.position = (start + end) * 0.5f;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);
            var sourceSize = beamRenderer.sprite.bounds.size;
            transform.localScale = new Vector3(distance / sourceSize.x, height / sourceSize.y, 1f);
        }
    }

    // Tints a selected chain planet with the icy blue-white palette of the lightning ball.
    // It stays active until that planet is deleted at the end of the chain.
    public sealed class LightningChargeVisual : MonoBehaviour
    {
        private SpriteRenderer targetRenderer;
        private Color originalColor;
        private bool configured;

        public void Configure(SpriteRenderer renderer)
        {
            targetRenderer = renderer;
            if (targetRenderer == null) return;
            originalColor = targetRenderer.color;
            configured = true;
        }

        private void Update()
        {
            if (!configured || targetRenderer == null) return;
            var pulse = 0.55f + Mathf.PingPong(Time.time * 10f, 0.35f);
            var electricBlue = new Color(0.48f, 0.9f, 1f, originalColor.a);
            targetRenderer.color = Color.Lerp(originalColor, electricBlue, pulse);
        }
    }

    // Shows the Q ability hint only while an unused black hole remains on the field.
    // It lives at the centre of the player's launch area rather than following a ball.
    public sealed class BlackHoleQPrompt : MonoBehaviour
    {
        private const float LaunchAreaCentreY = -4f;
        private const float PromptScale = 3f;
        // 블랙홀 Q 안내 이미지의 투명도입니다. 0은 완전 투명, 1은 완전 불투명입니다.
        private const float PromptAlpha = 0.75f;
        private const float AnimationCycleSeconds = 0.64f;
        private const float PressedFrameSeconds = 0.14f;
        private Sprite idleSprite;
        private Sprite pressedSprite;
        private SpriteRenderer promptRenderer;
        private static BlackHoleQPrompt instance;
        private float forcedPressedUntil;

        public static void ShowPressedFrame()
        {
            if (instance != null) instance.forcedPressedUntil = Time.time + PressedFrameSeconds;
        }

        private void Awake()
        {
            instance = this;
            idleSprite = CreateResourceSprite("UI/BlackHoleQIdle");
            pressedSprite = CreateResourceSprite("UI/BlackHoleQPressed");
            promptRenderer = gameObject.AddComponent<SpriteRenderer>();
            promptRenderer.sortingOrder = 14;
            promptRenderer.color = new Color(1f, 1f, 1f, PromptAlpha);
            transform.position = new Vector2(0f, LaunchAreaCentreY);
            transform.localScale = Vector3.one * PromptScale;
        }

        private void Update()
        {
            var blackHoleReady = Object.FindObjectsByType<Ball>(FindObjectsSortMode.None)
                .Any(ball => ball.CanActivateBlackHoleRing);
            if (promptRenderer == null) return;
            var showFinalPressedFrame = Time.time < forcedPressedUntil;
            promptRenderer.enabled = blackHoleReady || showFinalPressedFrame;
            if (!promptRenderer.enabled) return;

            // Flash the pressed art briefly each cycle so the player notices that Q
            // is an available action; the real Q key-down always selects it as well.
            var frameTime = Mathf.Repeat(Time.time, AnimationCycleSeconds);
            var showPressed = showFinalPressedFrame || Input.GetKey(KeyCode.Q) || frameTime >= AnimationCycleSeconds - PressedFrameSeconds;
            promptRenderer.sprite = showPressed && pressedSprite != null ? pressedSprite : idleSprite;
        }

        private static Sprite CreateResourceSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.height);
        }
    }

    public sealed class BowlingCamera : MonoBehaviour
    {
        private static float shakeUntil;
        private static float shakeAmount;
        public static void Kick(float amount) { shakeUntil = Time.time + 0.09f; shakeAmount = amount; }
        private void LateUpdate()
        {
            transform.position = Time.time < shakeUntil ? new Vector3(Random.insideUnitCircle.x * shakeAmount, Random.insideUnitCircle.y * shakeAmount, -10f) : new Vector3(0f, 0f, -10f);
        }
    }

    // Rotates and visibly expands/contracts the radial rainbow sunlight left with the clear reward.
    public sealed class VictoryRainbowHalo : MonoBehaviour
    {
        // Adjust these two values to change the clear-effect motion.
        private const float RotationDegreesPerSecond = -88f;
        private const float PulseAmount = 0.32f;
        private SpriteRenderer haloRenderer;
        private float baseScale = 1f;

        public void Configure(SpriteRenderer renderer, float scale)
        {
            haloRenderer = renderer;
            baseScale = scale;
            transform.localScale = Vector3.one * baseScale;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, RotationDegreesPerSecond * Time.deltaTime);
            var pulse = 1f + Mathf.Sin(Time.time * 7f) * PulseAmount;
            transform.localScale = Vector3.one * (baseScale * pulse);
            if (haloRenderer != null)
                haloRenderer.color = new Color(1f, 1f, 1f, 0.72f + Mathf.PingPong(Time.time * 3.6f, 0.28f));
        }
    }

    public sealed class BowlingHUD : MonoBehaviour
    {
        // HUD 배치/크기는 이 값들만 바꾸면 됩니다.
        private const float HudMargin = 28f;
        // SCORE 표시 폭과 맞춘 RANKING 패널의 가로 폭입니다.
        private const float RankingPanelWidth = 240f;
        // 중앙 레일 왼쪽 빈 공간의 폭 비율입니다. SCORE와 RANKING의 가로 중앙 기준입니다.
        private const float LeftHudAreaWidthRatio = 0.28f;
        private const float RankingHeightRatio = 0.30f;
        private const float NextPreviewSize = 108f;
        private const float ThenPreviewSize = 82f;
        private const float PreviewGap = 30f;
        private const float LifeIconSize = 50f;
        private const float LifeIconGap = 24f;
        // 다음 공 미리보기는 레일 밖의 오른쪽 여백에만 표시합니다.
        // 값을 키우면 왼쪽(레일 쪽), 작게 하면 오른쪽으로 이동합니다.
        private const float PreviewGroupLeftOffset = 0f;
        // 보스전 시작 자막: 화면 가로의 약 2/3을 사용합니다.
        // 위치는 BossIntroYRatio, 글자 크기는 BossIntroFontSize로 조절하세요.
        private const float BossIntroWidthRatio = 0.8f;
        private const float BossIntroYRatio = 0.2f;
        private const float BossIntroHeight = 92f;
        private const int BossIntroFontSize = 100;
        private const float BossIntroFadeSeconds = 2.2f;
        // 50K 경보 자막/화면 효과입니다. 글자는 BossWarningCharacterInterval 간격으로 한 글자씩 표시됩니다.
        private const string BossWarningText = "타일 크랩을 물리치세요";
        private const float BossWarningCharacterInterval = 0.12f;
        private const float BossWarningYRatio = 0.43f;
        private const float BossWarningHeight = 110f;
        private const int BossWarningFontSize = 92;
        // 보스 타이머 위치/크기입니다. y값을 키우면 아래로 이동합니다.
        private const float BossTimerY = 40f;
        private const float BossTimerHeight = 200f;
        private const int BossTimerFontSize = 200;
        private GUIStyle label;
        private GUIStyle scoreLabel;
        private GUIStyle smallLabel;
        private GUIStyle panelStyle;
        private GUIStyle bossIntroLabel;
        private GUIStyle bossTimerLabel;
        private GUIStyle bossWarningLabel;
        private Texture2D fullHeartTexture;
        private Texture2D emptyHeartTexture;
        private bool wasBossActive;
        private float bossIntroUntil;
        private void OnGUI()
        {
            var game = Object.FindFirstObjectByType<BowlingSuikaGame>();
            if (game == null) return;
            label ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 34, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            scoreLabel ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 34, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            smallLabel ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 18, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            panelStyle ??= new GUIStyle(GUI.skin.box) { font = BowlingSuikaGame.PixelFont, fontSize = 22, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperCenter };
            bossIntroLabel ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = BossIntroFontSize, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            bossTimerLabel ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = BossTimerFontSize, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.58f) } };
            bossWarningLabel ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = BossWarningFontSize, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            // The warning phase now owns the boss-start announcement, before the timer begins.
            if (game.BossActive && !wasBossActive) bossIntroUntil = 0f;
            wasBossActive = game.BossActive;
            var rankingPanel = DrawLeaderboard();
            GUI.Label(new Rect(rankingPanel.x, rankingPanel.y - 52f, rankingPanel.width, 42f), $"SCORE  {game.Score}", scoreLabel);
            var nextRect = new Rect(Screen.width - HudMargin - PreviewGroupLeftOffset - ThenPreviewSize - PreviewGap - NextPreviewSize,
                Screen.height - HudMargin - NextPreviewSize, NextPreviewSize, NextPreviewSize);
            var thenRect = new Rect(Screen.width - HudMargin - PreviewGroupLeftOffset - ThenPreviewSize,
                Screen.height - HudMargin - ThenPreviewSize, ThenPreviewSize, ThenPreviewSize);
            DrawLives(nextRect, thenRect, game.Lives, game.MaxLives);
            DrawUpcomingBall(nextRect, game.NextTier, "다음");
            DrawUpcomingBall(thenRect, game.FollowingTier, "다다음");
            if (game.BossActive && !game.GameOver)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 120f, BossTimerY, 240f, BossTimerHeight),
                    game.BossCountdownSeconds.ToString(), bossTimerLabel);
            }
            if (bossIntroUntil > Time.unscaledTime)
            {
                var alpha = Mathf.Clamp01((bossIntroUntil - Time.unscaledTime) / BossIntroFadeSeconds);
                var originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                var width = Screen.width * BossIntroWidthRatio;
                GUI.Label(new Rect((Screen.width - width) * 0.5f, Screen.height * BossIntroYRatio, width, BossIntroHeight),
                    "우주 괴물을 물리치세요", bossIntroLabel);
                GUI.color = originalColor;
            }
            if (game.BossWarningActive) DrawBossWarning(game);
            if (game.GameOver)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 185, Screen.height * 0.5f - 88, 370, 176), game.GameClear ? "GAME CLEAR" : "GAME OVER", panelStyle);
                GUI.Label(new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f - 35, 340, 30), $"FINAL SCORE  {game.Score}", label);
                GUI.Label(new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f + 10, 340, 30), "Press R to restart", label);
                GUI.Label(new Rect(Screen.width * 0.5f - 170, Screen.height * 0.5f + 43, 340, 30), "Press Esc to return Home", smallLabel);
            }
        }

        private void DrawBossWarning(BowlingSuikaGame game)
        {
            // Pulsing transparent red covers the full game canvas without changing its aspect ratio.
            var pulse = 0.18f + Mathf.PingPong(Time.unscaledTime * 3.8f, 0.34f);
            var originalColor = GUI.color;
            GUI.color = new Color(1f, 0.02f, 0.02f, pulse);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            var characterCount = Mathf.Clamp(Mathf.FloorToInt(game.BossWarningElapsed / BossWarningCharacterInterval) + 1,
                0, BossWarningText.Length);
            var typedText = BossWarningText.Substring(0, characterCount);
            if (characterCount < BossWarningText.Length && Mathf.Repeat(Time.unscaledTime, 0.35f) < 0.18f) typedText += "_";
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * BossWarningYRatio, Screen.width * 0.8f, BossWarningHeight),
                typedText, bossWarningLabel);
            GUI.color = originalColor;
        }

        private Rect DrawLeaderboard()
        {
            var entries = LocalLeaderboard.Load();
            var panelWidth = RankingPanelWidth;
            var panelHeight = Mathf.Max(250f, Screen.height * RankingHeightRatio);
            var leftHudCenter = Screen.width * LeftHudAreaWidthRatio * 0.5f;
            var panel = new Rect(leftHudCenter - panelWidth * 0.5f, Screen.height - HudMargin - panelHeight, panelWidth, panelHeight);
            GUI.Box(panel, "RANKING", panelStyle);
            if (entries.Count == 0)
            {
                GUI.Label(new Rect(panel.x + 18, panel.y + 72, panel.width - 36, 32), "No records yet", smallLabel);
                return panel;
            }
            for (var i = 0; i < Mathf.Min(entries.Count, 5); i++)
            {
                var entry = entries[i];
                GUI.Label(new Rect(panel.x + 18, panel.y + 56 + i * 34, panel.width - 36, 30), $"{i + 1}. {entry.Name}   {entry.Score}", smallLabel);
            }
            return panel;
        }

        private void DrawUpcomingBall(Rect ballRect, int tier, string caption)
        {
            // Pixel font glyphs are taller than the old 17px label rect, which clipped their tops.
            GUI.Label(new Rect(ballRect.x - 6, ballRect.y - 33, ballRect.width + 12, 30), caption, smallLabel);
            var originalColor = GUI.color;
            var sprite = BowlingSuikaGame.GetBallSprite(tier);
            if (sprite == BowlingSuikaGame.CircleSprite)
            {
                GUI.color = BowlingSuikaGame.BallColors[tier];
                GUI.DrawTexture(ballRect, sprite.texture, ScaleMode.StretchToFill, true);
            }
            else
            {
                var rect = sprite.textureRect;
                var texture = sprite.texture;
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(ballRect, texture, new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height), true);
            }
            GUI.color = originalColor;
            if (sprite == BowlingSuikaGame.CircleSprite && BowlingSuikaGame.GlossyMarbleSprite != null && tier != BowlingSuikaGame.BlackHoleTier)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.38f);
                GUI.DrawTexture(ballRect, BowlingSuikaGame.GlossyMarbleSprite.texture, ScaleMode.StretchToFill, true);
                GUI.color = originalColor;
            }
        }

        private void DrawLives(Rect nextRect, Rect thenRect, int lives, int maxLives)
        {
            fullHeartTexture ??= Resources.Load<Texture2D>("UI/HeartFull");
            emptyHeartTexture ??= Resources.Load<Texture2D>("UI/HeartEmpty");
            if (fullHeartTexture == null || emptyHeartTexture == null) return;

            var groupLeft = nextRect.x;
            var groupRight = thenRect.xMax;
            var lifeIconCount = Mathf.Clamp(maxLives, 1, 3);
            var groupWidth = LifeIconSize * lifeIconCount + LifeIconGap * (lifeIconCount - 1);
            var startX = groupLeft + (groupRight - groupLeft - groupWidth) * 0.5f;
            var y = Mathf.Max(HudMargin, nextRect.y - 96f);
            // The right heart is lost first, matching the requested life order.
            for (var index = 0; index < lifeIconCount; index++)
            {
                var heart = lives >= index + 1 ? fullHeartTexture : emptyHeartTexture;
                GUI.DrawTexture(new Rect(startX + index * (LifeIconSize + LifeIconGap), y, LifeIconSize, LifeIconSize), heart, ScaleMode.ScaleToFit, true);
            }
        }
    }

    public sealed class HomeSceneUI : MonoBehaviour
    {
        // Change this value to resize the infinite-mode checkbox.
        private const float InfiniteCheckboxSize = 50f;
        private bool showHelp;
        private bool showCredits;
        private bool showDifficulty;
        private bool focusNameField;
        private string enteredName;
        private float nameHintUntil;
        private int pendingStartingLives;
        private bool pendingInfiniteMode;
        private Texture2D backgroundTexture;
        private GUIStyle titleStyle;
        private GUIStyle titleShadowStyle;
        private GUIStyle buttonStyle;
        private GUIStyle helpStyle;
        private GUIStyle nameFieldStyle;
        private GUIStyle namePlaceholderStyle;
        private GUIStyle panelStyle;
        private GUIStyle difficultyValueStyle;
        private Texture2D popupPanelBackground;
        private Texture2D buttonNormalBackground;
        private Texture2D buttonHoverBackground;
        private Texture2D buttonActiveBackground;

        private void Awake()
        {
            // HomeScene supports IME input, while SampleScene explicitly turns it off in its Awake.
            Input.imeCompositionMode = IMECompositionMode.Auto;
            backgroundTexture = Resources.Load<Texture2D>("Backgrounds/Background");
            buttonNormalBackground = CreateSolidTexture(new Color(0.10f, 0.11f, 0.15f, 0.98f));
            buttonHoverBackground = CreateSolidTexture(new Color(0.15f, 0.30f, 0.50f, 1f));
            buttonActiveBackground = CreateSolidTexture(new Color(0.08f, 0.48f, 0.72f, 1f));
            popupPanelBackground = CreateSolidTexture(new Color(0.17f, 0.18f, 0.21f, 0.98f));
            enteredName = string.Empty;
            focusNameField = false;
            var homeCamera = Camera.main;
            if (homeCamera == null)
            {
                var cameraObject = new GameObject("Home Camera");
                cameraObject.tag = "MainCamera";
                homeCamera = cameraObject.AddComponent<Camera>();
                homeCamera.orthographic = true;
                homeCamera.clearFlags = CameraClearFlags.SolidColor;
                homeCamera.backgroundColor = new Color(0.02f, 0.04f, 0.08f);
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }
            homeCamera.orthographic = true;
            homeCamera.clearFlags = CameraClearFlags.SolidColor;
            homeCamera.backgroundColor = new Color(0.02f, 0.04f, 0.08f);
            // An AudioSource is silent unless the active scene has an AudioListener.
            if (homeCamera.GetComponent<AudioListener>() == null)
                homeCamera.gameObject.AddComponent<AudioListener>();

            CreateHomeBackground(homeCamera);
            gameObject.AddComponent<SoundSettingsUI>();
            new GameObject("Home Background Music").AddComponent<HomeBackgroundMusic>();
        }

        private void OnDisable()
        {
            // IME is a global input state; do not let a focused HomeScene text field capture
            // movement and action keys after this scene is unloaded.
            Input.imeCompositionMode = IMECompositionMode.Off;
        }

        private void CreateHomeBackground(Camera homeCamera)
        {
            if (backgroundTexture == null || GameObject.Find("Home Background") != null) return;

            var sprite = Sprite.Create(backgroundTexture,
                new Rect(0, 0, backgroundTexture.width, backgroundTexture.height),
                Vector2.one * 0.5f, backgroundTexture.width);
            var background = new GameObject("Home Background");
            background.transform.position = Vector3.zero;
            var renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;

            var visibleHeight = homeCamera.orthographicSize * 2f;
            var visibleWidth = visibleHeight * homeCamera.aspect;
            background.transform.localScale = new Vector3(
                visibleWidth / sprite.bounds.size.x,
                visibleHeight / sprite.bounds.size.y,
                1f);
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            // SoundSettingsUI uses depth -1000. Give every HomeScene control an
            // explicit lower-priority depth so the SOUND window covers the title,
            // name field, and menu buttons in both Editor and WebGL builds.
            GUI.depth = 1000;
            // Green land sits behind the blue ocean text, giving the logo an Earth palette.
            titleStyle ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 54, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.23f, 0.71f, 1f) } };
            titleShadowStyle ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 54, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.10f, 0.48f, 0.20f) } };
            // WebGL does not inherit the Unity Editor skin's light button text color.
            // Set it for every state so labels remain readable on the dark button face.
            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                font = BowlingSuikaGame.PixelFont,
                fontSize = 24,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = buttonNormalBackground },
                hover = { textColor = Color.white, background = buttonHoverBackground },
                active = { textColor = Color.white, background = buttonActiveBackground },
                focused = { textColor = Color.white, background = buttonNormalBackground }
            };
            helpStyle ??= new GUIStyle(GUI.skin.label) { font = BowlingSuikaGame.PixelFont, fontSize = 18, fontStyle = FontStyle.Normal, wordWrap = true, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            nameFieldStyle ??= new GUIStyle(GUI.skin.textField)
            {
                font = BowlingSuikaGame.PixelFont,
                fontSize = 25,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 12, 7, 6),
                normal = { textColor = new Color(0.04f, 0.12f, 0.24f), background = Texture2D.whiteTexture },
                hover = { textColor = new Color(0.04f, 0.12f, 0.24f), background = Texture2D.whiteTexture },
                focused = { textColor = new Color(0.04f, 0.12f, 0.24f), background = Texture2D.whiteTexture },
                active = { textColor = new Color(0.04f, 0.12f, 0.24f), background = Texture2D.whiteTexture }
            };
            namePlaceholderStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = BowlingSuikaGame.PixelFont,
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 12, 7, 6),
                normal = { textColor = new Color(0.20f, 0.34f, 0.50f, 1f) }
            };
            panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                font = BowlingSuikaGame.PixelFont,
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white, background = popupPanelBackground }
            };
            difficultyValueStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = BowlingSuikaGame.PixelFont,
                fontSize = 24,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            var centerX = Screen.width * 0.5f;
            if (showHelp)
            {
                DrawHelpPopup(centerX);
                return;
            }
            if (showCredits)
            {
                DrawCreditsPopup(centerX);
                return;
            }
            if (showDifficulty)
            {
                DrawDifficultyPopup(centerX);
                return;
            }

            var titleRect = new Rect(centerX - 300, Screen.height * 0.22f, 600, 75);
            var originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(centerX, titleRect.y, 0f), Quaternion.identity, new Vector3(1.5f, 1.3f, 1f))
                         * Matrix4x4.TRS(new Vector3(-centerX, -titleRect.y, 0f), Quaternion.identity, Vector3.one);
            GUI.Label(new Rect(titleRect.x + 3f, titleRect.y + 4f, titleRect.width, titleRect.height), "PLANET SHOT", titleShadowStyle);
            GUI.Label(titleRect, "PLANET SHOT", titleStyle);
            GUI.matrix = originalMatrix;
            var nameRect = new Rect(centerX - 135, Screen.height * 0.42f, 270, 44);
            var nameBorderRect = new Rect(nameRect.x - 3f, nameRect.y - 3f, nameRect.width + 6f, nameRect.height + 6f);
            var previousGuiColor = GUI.color;
            GUI.color = new Color(0.20f, 0.78f, 1f, 0.98f);
            GUI.DrawTexture(nameBorderRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.88f, 0.97f, 1f, 1f);
            GUI.DrawTexture(nameRect, Texture2D.whiteTexture);
            GUI.color = previousGuiColor;
            GUI.SetNextControlName("HomePlayerNameField");
            enteredName = GUI.TextField(nameRect, enteredName, nameFieldStyle);
            var nameFieldFocused = GUI.GetNameOfFocusedControl() == "HomePlayerNameField";
            Input.imeCompositionMode = nameFieldFocused ? IMECompositionMode.On : IMECompositionMode.Auto;
            if (string.IsNullOrWhiteSpace(enteredName) && !nameFieldFocused)
                GUI.Label(nameRect, "이름을 입력하세요", namePlaceholderStyle);
            if (Time.unscaledTime < nameHintUntil && string.IsNullOrWhiteSpace(enteredName))
            {
                var alpha = Mathf.Clamp01((nameHintUntil - Time.unscaledTime) / 1.2f);
                var originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(titleRect.x, titleRect.yMax + 8f, titleRect.width, titleRect.height), "이름을 입력하세요", titleStyle);
                GUI.color = originalColor;
            }
            if (GUI.Button(new Rect(centerX - 135, Screen.height * 0.50f, 270, 58), "게임 시작", buttonStyle))
            {
                if (string.IsNullOrWhiteSpace(enteredName)) nameHintUntil = Time.unscaledTime + 1.2f;
                else
                {
                    PlayerProfile.SetName(enteredName);
                    Input.imeCompositionMode = IMECompositionMode.Off;
                    SceneManager.LoadScene("SampleScene");
                }
            }
            if (GUI.Button(new Rect(centerX - 135, Screen.height * 0.50f + 78, 270, 58), "난이도", buttonStyle))
            {
                pendingStartingLives = GameDifficultySettings.StartingLives;
                pendingInfiniteMode = GameDifficultySettings.InfiniteMode;
                showDifficulty = true;
            }
            if (GUI.Button(new Rect(centerX - 135, Screen.height * 0.50f + 156, 270, 58), "도움말", buttonStyle))
                showHelp = true;
            if (GUI.Button(new Rect(centerX - 135, Screen.height * 0.50f + 234, 270, 58), "CREDIT", buttonStyle))
                showCredits = true;
        }

        private void DrawDifficultyPopup(float centerX)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var popup = new Rect(centerX - 280, Screen.height * 0.23f, 560, 350);
            GUI.Box(popup, "난이도 설정", panelStyle);
            GUI.Label(new Rect(popup.x + 48, popup.y + 64, popup.width - 96, 34), "시작 HP", helpStyle);
            if (GUI.Button(new Rect(centerX - 112, popup.y + 60, 58, 42), "-", buttonStyle))
                pendingStartingLives = Mathf.Max(1, pendingStartingLives - 1);
            GUI.Label(new Rect(centerX - 48, popup.y + 63, 96, 36), pendingStartingLives.ToString(), difficultyValueStyle);
            if (GUI.Button(new Rect(centerX + 54, popup.y + 60, 58, 42), "+", buttonStyle))
                pendingStartingLives = Mathf.Min(3, pendingStartingLives + 1);

            GUI.Label(new Rect(popup.x + 48, popup.y + 156, 140, 34), "무한 모드", helpStyle);
            var checkboxRect = new Rect(popup.x + 194, popup.y + 136, InfiniteCheckboxSize, InfiniteCheckboxSize);
            var previousColor = GUI.color;
            GUI.color = pendingInfiniteMode ? new Color(0.15f, 0.78f, 1f, 1f) : new Color(0.38f, 0.43f, 0.52f, 1f);
            GUI.DrawTexture(checkboxRect, Texture2D.whiteTexture);
            var checkboxInset = new Rect(checkboxRect.x + 6f, checkboxRect.y + 6f, checkboxRect.width - 12f, checkboxRect.height - 12f);
            GUI.color = pendingInfiniteMode ? new Color(0.05f, 0.34f, 0.78f, 1f) : new Color(0.06f, 0.08f, 0.14f, 1f);
            GUI.DrawTexture(checkboxInset, Texture2D.whiteTexture);
            GUI.color = previousColor;
            if (GUI.Button(checkboxRect, GUIContent.none, GUIStyle.none))
                pendingInfiniteMode = !pendingInfiniteMode;
            GUI.Label(new Rect(popup.x + 48, popup.y + 220, popup.width - 96, 58),
                "모든 벽이 없어지며 무한히 점수를 얻을 수 있습니다", helpStyle);

            if (GUI.Button(new Rect(centerX - 86, popup.yMax - 50, 172, 40), "확인", buttonStyle))
            {
                GameDifficultySettings.Apply(pendingStartingLives, pendingInfiniteMode);
                showDifficulty = false;
            }
        }

        private void DrawNamePopup(float centerX)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var popup = new Rect(centerX - 245, Screen.height * 0.32f, 490, 210);
            GUI.Box(popup, "환영합니다", panelStyle);
            GUI.Label(new Rect(popup.x + 35, popup.y + 48, popup.width - 70, 32), "순위에 기록할 이름을 입력하세요.", helpStyle);
            GUI.SetNextControlName("PlayerNameField");
            enteredName = GUI.TextField(new Rect(popup.x + 65, popup.y + 94, popup.width - 130, 44), enteredName, nameFieldStyle);
            if (focusNameField && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("PlayerNameField");
                focusNameField = false;
            }
            GUI.enabled = !string.IsNullOrWhiteSpace(enteredName);
            if (GUI.Button(new Rect(centerX - 82, popup.yMax - 58, 164, 36), "시작하기", buttonStyle))
            {
                PlayerProfile.SetName(enteredName);
                Input.imeCompositionMode = IMECompositionMode.Off;
                SceneManager.LoadScene("SampleScene");
            }
            GUI.enabled = true;
        }

        private void DrawHelpPopup(float centerX)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var popup = new Rect(centerX - 310, Screen.height * 0.25f, 620, 330);
            GUI.Box(popup, "게임 방법", panelStyle);
            GUI.Label(new Rect(popup.x + 35, popup.y + 58, popup.width - 70, 200), "1. WASD로 발사 위치를 상하좌우로 조절하고, 마우스를 아래로 드래그한 뒤 놓아 발사합니다.\n\n2. 같은 색깔의 구슬이 부딪히면 하나로 합쳐지고 점수를 얻습니다.\n\n3. 구슬이 필드 밖으로 이탈하면 HP가 감소합니다.", helpStyle);
            if (GUI.Button(new Rect(centerX - 75, popup.yMax - 62, 150, 38), "닫기", buttonStyle))
                showHelp = false;
        }

        private void DrawCreditsPopup(float centerX)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var popup = new Rect(centerX - 310, Screen.height * 0.25f, 620, 330);
            GUI.Box(popup, "CREDIT", panelStyle);
            GUI.Label(new Rect(popup.x + 35, popup.y + 58, popup.width - 70, 200),
                "Free jump by TADSource:\nhttps://opengameart.org/content/8-bit-pack-2\nLicensed under CC BY 4.0\nModified for PLANET SHOT",
                helpStyle);
            if (GUI.Button(new Rect(centerX - 75, popup.yMax - 62, 150, 38), "닫기", buttonStyle))
                showCredits = false;
        }
    }
}
