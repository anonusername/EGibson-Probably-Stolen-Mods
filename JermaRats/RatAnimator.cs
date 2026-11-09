using UnityEngine;

namespace JermaRats
{
    /// <summary>
    /// Lives on a rat's GameItemElement.handler. Moves only the picture: the background image and its drag-ghost twin
    /// (fakeBackground). The game rewrites rotator/middle transforms itself, but only ever writes background's
    /// anchoredPosition, to (modifiedXOrigin, modifiedYOrigin) * gridSize, and never its scale or rotation.
    /// Frames are shown with GameItemElement.ApplyAnimationFrame, which keeps the vanilla spritePath, so the game's
    /// nightly size change, death sprite and saving keep working. A hidden cage window deactivates this object, so
    /// hidden rats cost nothing. No coroutines (scanner: StartCoroutine only on this, and none are needed).
    /// </summary>
    public class RatAnimator : MonoBehaviour
    {
        private const float BreathPeriod = 1.6f;
        private const int FrameMs = 150;

        /// <summary>Set when the champion's element is destroyed; the next live animator queues a rescan.</summary>
        private static bool championLost;

        private GameItemElement element;
        private float phase;
        private float nextTwitch;
        private float twitchStart = -10f;
        private bool wasHeld;
        private bool wasChampion;
        private bool deadHandled;
        private bool posed;

        private Sprite vanillaSprite;
        private string vanillaPath;
        private Sprite lastFrame;

        public void Bind(GameItemElement el)
        {
            element = el;
            phase = Random.value * 10f;
            nextTwitch = Time.unscaledTime + Random.Range(2f, 6f);
        }

        private void Update()
        {
            if (element == null || element.IsDestroyed())
            {
                Destroy(this);
                return;
            }
            if (element.isDebugMenu)
            {
                Destroy(this);
                return;
            }
            if (championLost)
            {
                championLost = false;
                BiggestRat.QueueRescan();
            }

            bool held = IsHeld();
            if (held && !wasHeld)
            {
                RatExtras.OnPickedUp(element);
            }
            wasHeld = held;
        }

        private void LateUpdate()
        {
            if (element == null || element.background == null || element.IsDestroyed())
            {
                return;
            }

            bool dead = element.IsTag("ANIMAL_IS_DEAD_TAG");
            bool champion = BiggestRat.IsChampion(element);
            wasChampion = champion;

            if (dead)
            {
                if (!deadHandled)
                {
                    deadHandled = true;
                    ResetPose();
                    lastFrame = null; // stop touching the sprite: the game swaps in the dead picture itself
                    if (champion)
                    {
                        BiggestRat.QueueRescan();
                    }
                }
                return;
            }
            deadHandled = false;

            UpdateFrames(champion);
            UpdatePose(champion);
        }

        private void OnDestroy()
        {
            // Never call into the game or the loader from here; just leave a note for the next Update.
            if (wasChampion && !deadHandled)
            {
                championLost = true;
            }
        }

        private bool IsHeld()
        {
            ItemMouseDragHandler drag = ItemMouseDragHandler.current;
            return drag != null && drag.IsDraggingItem && drag.GetCurrentItem() == element;
        }

        // ---------------------------------------------------------------- frames

        private void UpdateFrames(bool champion)
        {
            UnityEngine.UI.Image image = element.background.image;
            if (image == null)
            {
                return;
            }

            // A sprite we didn't put there is the game's own (initial, nightly resize, load) - but only once it matches
            // the item's spritePath. During a save load spritePath changes before the image is re-resolved.
            Sprite shown = image.sprite;
            if (shown != null && shown != lastFrame && shown != vanillaSprite && !RatFrames.IsOurs(shown)
                && shown.name == element.spritePath)
            {
                vanillaSprite = shown;
                vanillaPath = element.spritePath;
                ParsePath(vanillaPath);
            }

            Sprite[] frames = ChooseFrames(champion);
            if (frames.Length == 0)
            {
                // Hand the picture back to the game if one of our frames is still up and the sprite didn't change.
                if (lastFrame != null && image.sprite == lastFrame && vanillaSprite != null && element.spritePath == vanillaPath)
                {
                    element.ApplyAnimationFrame(vanillaSprite);
                }
                lastFrame = null;
                return;
            }

            float speed = ModEntry.AnimSpeed != null ? Mathf.Max(0.05f, ModEntry.AnimSpeed.Value) : 1f;
            float frameSeconds = FrameMs / 1000f / speed;
            float offset = champion ? 0f : phase;
            int index = (int)((Time.unscaledTime + offset) / frameSeconds) % frames.Length;
            Sprite frame = frames[index];
            if (image.sprite != frame)
            {
                element.ApplyAnimationFrame(frame);
            }
            lastFrame = frame;
        }

        // Parsed once per vanilla sprite change (rat_<size>_<m|f>), and the frame sets cached per change, so the
        // per-frame path allocates nothing.
        private string size;
        private string sex;
        private Sprite[] championFrames;
        private Sprite[] normalFrames;
        private int framesVersion = -1;

        private void ParsePath(string path)
        {
            size = null;
            sex = null;
            framesVersion = -1;
            if (path == null || !path.StartsWith("rat_"))
            {
                return;
            }
            string[] parts = path.Split('_');
            if (parts.Length == 3)
            {
                size = parts[1];
                sex = parts[2];
            }
        }

        private Sprite[] ChooseFrames(bool champion)
        {
            // Wait until the game has finished any sprite change, so frames always match the current footprint.
            if (vanillaSprite == null || size == null || element.spritePath != vanillaPath)
            {
                return RatFrames.Empty;
            }
            if (framesVersion != RatFrames.Version)
            {
                framesVersion = RatFrames.Version;
                championFrames = RatFrames.For(size, sex, true);
                normalFrames = RatFrames.For(size, sex, false);
                RatFrames.CheckSize(championFrames, vanillaSprite);
                RatFrames.CheckSize(normalFrames, vanillaSprite);
            }
            bool useChampion = champion && ModEntry.ChampionGraphic != null && ModEntry.ChampionGraphic.Value;
            if (useChampion)
            {
                return championFrames;
            }
            return ModEntry.Animate == null || ModEntry.Animate.Value ? normalFrames : RatFrames.Empty;
        }

        // ---------------------------------------------------------------- motion

        private void UpdatePose(bool champion)
        {
            bool animate = ModEntry.Animate == null || ModEntry.Animate.Value;
            float t = Time.unscaledTime;
            float speed = ModEntry.AnimSpeed != null ? Mathf.Max(0.05f, ModEntry.AnimSpeed.Value) : 1f;
            bool rotationOnly = ModEntry.RotationOnly != null && ModEntry.RotationOnly.Value;

            Vector2 scale = Vector2.one;
            float angle = 0f;

            if (RatExtras.IsCelebrating(element))
            {
                angle = 10f * Mathf.Sin(t * Mathf.PI * 2f * 2.5f);
                float hop = 0.08f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 2.5f));
                scale = new Vector2(1f + hop, 1f + hop);
            }
            else if (animate)
            {
                if (wasHeld)
                {
                    float w = Mathf.Sin(t * Mathf.PI * 2f * 8f);
                    angle = 4f * Mathf.Sin(t * Mathf.PI * 2f * 6f);
                    scale = new Vector2(1f + 0.06f * w, 1f - 0.06f * w);
                }
                else
                {
                    float breath = Mathf.Sin((t * speed / BreathPeriod + phase) * Mathf.PI * 2f);
                    scale = new Vector2(1f, 1f + 0.03f * breath);
                    if (t >= nextTwitch)
                    {
                        twitchStart = t;
                        nextTwitch = t + Random.Range(2.5f, 7f) / speed;
                    }
                    float since = t - twitchStart;
                    if (since < 0.25f)
                    {
                        angle = 2f * Mathf.Sin(since / 0.25f * Mathf.PI * 2f);
                    }
                }
            }

            if (rotationOnly)
            {
                scale = Vector2.one;
            }

            if (scale == Vector2.one && angle == 0f)
            {
                if (posed)
                {
                    ResetPose();
                }
                return;
            }
            Apply(element.background.rectTransform, scale, angle);
            if (element.fakeBackground != null)
            {
                Apply(element.fakeBackground.rectTransform, scale, angle);
            }
            posed = true;
        }

        private Vector2 BasePosition()
        {
            int grid = UISettings.current != null ? UISettings.current.gridSize : 16;
            return new Vector2(element.modifiedXOrigin * grid, element.modifiedYOrigin * grid);
        }

        /// <summary>Scale and rotate about the sprite's centre, whatever the prefab's pivot is.</summary>
        private void Apply(RectTransform rt, Vector2 scale, float angle)
        {
            if (rt == null)
            {
                return;
            }
            Vector2 size = rt.rect.size;
            Vector2 c = new Vector2((0.5f - rt.pivot.x) * size.x, (0.5f - rt.pivot.y) * size.y);
            Vector2 sc = new Vector2(scale.x * c.x, scale.y * c.y);
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            Vector2 rotated = new Vector2(cos * sc.x - sin * sc.y, sin * sc.x + cos * sc.y);
            rt.anchoredPosition = BasePosition() + c - rotated;
            rt.localScale = new Vector3(scale.x, scale.y, 1f);
            rt.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        private void ResetPose()
        {
            posed = false;
            Vector2 basePos = BasePosition();
            Reset(element.background?.rectTransform, basePos);
            Reset(element.fakeBackground?.rectTransform, basePos);
        }

        private static void Reset(RectTransform rt, Vector2 basePos)
        {
            if (rt == null)
            {
                return;
            }
            rt.anchoredPosition = basePos;
            rt.localScale = Vector3.one;
            rt.localEulerAngles = Vector3.zero;
        }

        private void OnDisable()
        {
            if (element != null && !element.IsDestroyed() && posed)
            {
                ResetPose();
            }
        }
    }
}
