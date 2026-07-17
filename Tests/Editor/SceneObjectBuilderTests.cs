using System.Linq;
using Neo.UIKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Neo.UIKit.Tests
{
    /// <summary>Test page views resolved by the scene builder via reflection.</summary>
    public sealed class ScenetestmainView : UiPageBase
    {
    }

    /// <summary>Second test page view (non-start page).</summary>
    public sealed class ScenetestsecondView : UiPageBase
    {
    }

    /// <summary>Marker component used to verify user components survive rebuilds.</summary>
    public sealed class SceneBuilderTestMarker : MonoBehaviour
    {
    }

    public class SceneObjectBuilderTests
    {
        private UiKitConfig _config;

        [SetUp]
        public void CreateSceneAndConfig()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            _config = ScriptableObject.CreateInstance<UiKitConfig>();
            _config.generatorNamespace = "Neo.UIKit.Tests";
            _config.pages.Add(new UiKitConfig.PageEntry { pageId = "scenetestmain", isStart = true });
            _config.pages.Add(new UiKitConfig.PageEntry { pageId = "scenetestsecond" });
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_config);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private SceneBuildReport Build()
        {
            return SceneObjectBuilder.Build(_config, _config.generatorNamespace);
        }

        [Test]
        public void CreatesRootBootstrapAndPageObjects()
        {
            SceneBuildReport report = Build();

            Assert.AreEqual(2, report.Created.Count);
            Assert.IsEmpty(report.Errors);

            UiKitBootstrap[] bootstraps = Object.FindObjectsByType<UiKitBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, bootstraps.Length);
            Assert.AreEqual("UI", bootstraps[0].gameObject.name);
            Assert.AreEqual(_config, bootstraps[0].Config);

            UiPageBase[] pages = Object.FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(2, pages.Length);

            UiPageBase main = pages.First(p => p.PageId == "scenetestmain");
            UiPageBase second = pages.First(p => p.PageId == "scenetestsecond");
            Assert.IsInstanceOf<ScenetestmainView>(main);
            Assert.IsNotNull(main.GetComponent<PanelRenderer>());
            Assert.IsTrue(main.gameObject.activeSelf, "start page stays active");
            Assert.IsFalse(second.gameObject.activeSelf, "non-start pages are created inactive");
        }

        [Test]
        public void SecondBuildIsIdempotentAndKeepsUserChanges()
        {
            Build();

            UiPageBase page = Object.FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(p => p.PageId == "scenetestmain");
            page.gameObject.AddComponent<SceneBuilderTestMarker>();
            page.gameObject.name = "Renamed by user";
            page.transform.localPosition = new Vector3(1f, 2f, 3f);

            SceneBuildReport second = Build();

            Assert.IsEmpty(second.Created);
            Assert.AreEqual(2, second.Updated.Count);
            Assert.AreEqual(1, Object.FindObjectsByType<UiKitBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.AreEqual(2, Object.FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            UiPageBase samePage = Object.FindObjectsByType<UiPageBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(p => p.PageId == "scenetestmain");
            Assert.IsNotNull(samePage.GetComponent<SceneBuilderTestMarker>(), "user components survive");
            Assert.AreEqual("Renamed by user", samePage.gameObject.name, "idempotency is keyed by pageId, not name");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), samePage.transform.localPosition, "transform is untouched");
        }

        [Test]
        public void ScenePagesMissingFromConfigAreReportedNotDeleted()
        {
            Build();

            var ghost = new GameObject("ghost");
            var page = ghost.AddComponent<ScenetestmainView>();
            var serialized = new SerializedObject(page);
            serialized.FindProperty("pageId").stringValue = "ghostpage";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SceneBuildReport report = Build();

            Assert.Contains("ghostpage", report.Orphaned);
            Assert.IsNotNull(GameObject.Find("ghost"), "orphaned pages are never deleted");
        }

        [Test]
        public void MissingGeneratedTypeIsReportedAsError()
        {
            _config.pages.Add(new UiKitConfig.PageEntry { pageId = "nosuchpage" });

            SceneBuildReport report = Build();

            Assert.AreEqual(2, report.Created.Count);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("NosuchpageView")));
        }
    }
}
