using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit
{
    /// <summary>
    /// Fake loading driver for a loading page. Lives next to the page component (added to the
    /// start page by the scene builder by default): fills the page progress bar over a random
    /// duration with randomized chunky steps (like a real load), then shows the next page.
    /// </summary>
    [RequireComponent(typeof(UiPageBase))]
    public sealed class UiFakeLoading : MonoBehaviour
    {
        [Tooltip("Minimum total loading time in seconds.")]
        [SerializeField] private float minSeconds = 1f;
        [Tooltip("Maximum total loading time in seconds.")]
        [SerializeField] private float maxSeconds = 3f;
        [Tooltip("Page shown when loading completes; empty = \"mainmenu\" when registered, otherwise the first other page.")]
        [SerializeField] private string nextPageId = "";
        [Tooltip("Progress bar element path relative to the page; empty = first element with class \"fui_type_progressbar\".")]
        [SerializeField] private string progressBarPath = "";
        [Tooltip("Smallest single progress jump.")]
        [SerializeField] private float minStep = 0.03f;
        [Tooltip("Largest single progress jump.")]
        [SerializeField] private float maxStep = 0.25f;

        private readonly BarView _bar = new BarView();
        private UiPageBase _page;
        private Coroutine _run;

        /// <summary>Raised right before the next page is shown.</summary>
        public event Action Completed;

        /// <summary>Current progress 0..1.</summary>
        public float Progress { get; private set; }

        private void Awake()
        {
            _page = GetComponent<UiPageBase>();
        }

        private void OnEnable()
        {
            _run = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            if (_run != null)
            {
                StopCoroutine(_run);
                _run = null;
            }

            _bar.Unwire();
            Progress = 0f;
        }

        private IEnumerator Run()
        {
            while (!_page.IsBound)
                yield return null;

            BindBar();

            float duration = UnityEngine.Random.Range(minSeconds, Mathf.Max(minSeconds, maxSeconds));
            float elapsed = 0f;
            Progress = 0f;
            Apply(0f);

            while (Progress < 1f)
            {
                float pause = UnityEngine.Random.Range(0.08f, 0.35f);
                yield return new WaitForSecondsRealtime(pause);
                elapsed += pause;

                float target = Mathf.Clamp01(elapsed / duration);
                float step = UnityEngine.Random.Range(minStep, maxStep);
                Progress = elapsed >= duration ? 1f : Mathf.Clamp01(Mathf.Min(Progress + step, target));
                Apply(Progress);
            }

            yield return new WaitForSecondsRealtime(0.15f);

            Completed?.Invoke();
            string next = ResolveNextPage();
            if (!string.IsNullOrEmpty(next))
                UiKit.Pages.Show(next);
            _run = null;
        }

        private void BindBar()
        {
            VisualElement element = null;
            if (!string.IsNullOrEmpty(progressBarPath))
                element = _page.ResolveElement(progressBarPath);
            else if (_page.ScreenRoot != null)
                element = _page.ScreenRoot.Q<VisualElement>(className: "fui_type_progressbar");

            if (element != null)
                _bar.Bind(element);
            else
                Debug.LogWarning("[UiKit] UiFakeLoading found no progress bar on page '" + _page.PageId + "'.", this);
        }

        private void Apply(float value)
        {
            if (!_bar.IsBound && _page.IsBound)
                BindBar();
            if (_bar.IsBound)
                _bar.SetProgress(value);
        }

        private string ResolveNextPage()
        {
            if (!string.IsNullOrEmpty(nextPageId))
                return nextPageId;

            UiKitConfig config = UiKit.Config;
            if (config != null)
            {
                string firstOther = null;
                for (int i = 0; i < config.pages.Count; i++)
                {
                    UiKitConfig.PageEntry entry = config.pages[i];
                    if (entry == null || string.IsNullOrEmpty(entry.pageId) || entry.pageId == _page.PageId)
                        continue;
                    if (entry.pageId == "mainmenu")
                        return "mainmenu";
                    if (firstOther == null)
                        firstOther = entry.pageId;
                }

                if (firstOther != null)
                    return firstOther;
            }

            Debug.LogWarning("[UiKit] UiFakeLoading has no next page to show.", this);
            return null;
        }
    }
}
