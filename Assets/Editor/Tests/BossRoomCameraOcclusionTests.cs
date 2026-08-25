#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boss 房间摄像机穿墙回归测试：保护镜头避障过滤、遮挡墙隐藏恢复以及场景装配。
/// </summary>
public sealed class BossRoomCameraOcclusionTests
{
    private const string BossScenePath = "Assets/Scenes/BossRoomScene.unity";

    [Test]
    public void CameraCo_MarkedWallDoesNotPushCamera_NormalWallStillDoes()
    {
        GameObject targetObject = new GameObject("CameraTarget");
        GameObject cameraObject = new GameObject("Camera");
        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            CameraCo cameraController = cameraObject.AddComponent<CameraCo>();
            cameraController.target = targetObject.transform;
            cameraController.offset = Vector3.up;

            Vector3 pivotPosition = Vector3.up;
            Vector3 desiredPosition = new Vector3(0f, 1f, -10f);
            wallObject.transform.position = new Vector3(0f, 1f, -5f);
            wallObject.transform.localScale = new Vector3(4f, 4f, 0.5f);
            CameraPassThroughOccluder marker = wallObject.AddComponent<CameraPassThroughOccluder>();
            Physics.SyncTransforms();

            MethodInfo resolveMethod = typeof(CameraCo).GetMethod(
                "ResolveCameraPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

            Vector3 passThroughPosition = (Vector3)resolveMethod.Invoke(
                cameraController,
                new object[] { pivotPosition, desiredPosition });
            Assert.That(Vector3.Distance(passThroughPosition, desiredPosition), Is.LessThan(0.001f),
                "带穿墙标记的 Boss 墙不应把摄像机推回角色身边。");

            Object.DestroyImmediate(marker);
            Physics.SyncTransforms();
            Vector3 blockedPosition = (Vector3)resolveMethod.Invoke(
                cameraController,
                new object[] { pivotPosition, desiredPosition });
            Assert.That(Vector3.Distance(pivotPosition, blockedPosition), Is.LessThan(10f),
                "普通墙体仍应保留 CameraCo 原有避障，防止主场景行为被全局关闭。");
        }
        finally
        {
            Object.DestroyImmediate(wallObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void OcclusionController_HidesOnlyWhileWallBlocksView_AndKeepsCollider()
    {
        GameObject targetObject = new GameObject("CameraTarget");
        GameObject cameraObject = new GameObject("Camera");
        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            targetObject.transform.position = Vector3.zero;
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            wallObject.transform.position = new Vector3(0f, 1f, -5f);
            wallObject.transform.localScale = new Vector3(4f, 4f, 0.5f);

            CameraCo cameraController = cameraObject.AddComponent<CameraCo>();
            cameraController.target = targetObject.transform;
            cameraController.offset = Vector3.up;
            CameraOcclusionController occlusionController =
                cameraObject.AddComponent<CameraOcclusionController>();
            wallObject.AddComponent<CameraPassThroughOccluder>();

            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            Collider wallCollider = wallObject.GetComponent<Collider>();
            MethodInfo lateUpdateMethod = typeof(CameraOcclusionController).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo disableMethod = typeof(CameraOcclusionController).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdateMethod, Is.Not.Null);
            Assert.That(disableMethod, Is.Not.Null);

            Physics.SyncTransforms();
            lateUpdateMethod.Invoke(occlusionController, null);
            Assert.That(wallRenderer.forceRenderingOff, Is.True,
                "挡在角色与镜头之间的标记墙应临时停止渲染。");
            Assert.That(wallCollider.enabled, Is.True,
                "隐藏只应影响表现，墙体 Collider 必须继续限制战斗区域。");

            wallObject.transform.position = new Vector3(20f, 1f, -5f);
            Physics.SyncTransforms();
            lateUpdateMethod.Invoke(occlusionController, null);
            Assert.That(wallRenderer.forceRenderingOff, Is.False,
                "墙体不再遮挡时应恢复原始显示状态。");

            wallObject.transform.position = new Vector3(0f, 1f, -5f);
            Physics.SyncTransforms();
            lateUpdateMethod.Invoke(occlusionController, null);
            disableMethod.Invoke(occlusionController, null);
            Assert.That(wallRenderer.forceRenderingOff, Is.False,
                "切场景或禁用组件时必须恢复隐藏墙体。");
        }
        finally
        {
            Object.DestroyImmediate(wallObject);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void BossScene_MainCameraContainsOcclusionController()
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
            CameraCo[] cameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraCo>(true))
                .ToArray();

            Assert.That(cameras, Has.Length.EqualTo(1), "BossRoomScene 应且只能有一个玩法相机。");
            Assert.That(cameras[0].GetComponent<CameraOcclusionController>(), Is.Not.Null,
                "Boss 相机必须直接装配遮挡处理器，避免打包后依赖编辑器兜底。");
        }
        finally
        {
            if (scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
