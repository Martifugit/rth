using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Import rules for character art under Resources/Characters:
    /// <list type="bullet">
    /// <item>Body models (built by tools/character-models) import as Humanoid with our bone names
    /// mapped explicitly, so any humanoid animation retargets onto them.</item>
    /// <item>Files in Characters/Animations (e.g. Mixamo downloads, one clip per file, named by
    /// what they are: idle, walk, run, cast, hit, death) import as Humanoid clips. Idle, walk and run
    /// loop. Walk and run keep their forward motion so the game can read how fast they step; the
    /// game never applies root motion, the server moves entities.</item>
    /// </list>
    /// </summary>
    public sealed class CharacterImporter : AssetPostprocessor
    {
        private const string Root = "Assets/Resources/Characters/";
        private const string Animations = Root + "Animations/";

        // Our skeleton (see tools/character-models/build_humanoid.py) to Unity's human bones.
        private static readonly (string bone, string human)[] Map =
        {
            ("hips", "Hips"), ("spine", "Spine"), ("chest", "Chest"), ("neck", "Neck"), ("head", "Head"),
            ("upper_arm.L", "LeftUpperArm"), ("forearm.L", "LeftLowerArm"), ("hand.L", "LeftHand"),
            ("upper_arm.R", "RightUpperArm"), ("forearm.R", "RightLowerArm"), ("hand.R", "RightHand"),
            ("thigh.L", "LeftUpperLeg"), ("shin.L", "LeftLowerLeg"), ("foot.L", "LeftFoot"), ("toe.L", "LeftToes"),
            ("thigh.R", "RightUpperLeg"), ("shin.R", "RightLowerLeg"), ("foot.R", "RightFoot"), ("toe.R", "RightToes"),
        };

        private static bool IsLooping(string name) =>
            name.StartsWith("idle") || name.StartsWith("walk") || name.StartsWith("run");

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            bool animation = assetPath.StartsWith(Animations);
            importer.importAnimation = animation;
            if (animation) importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Files on our skeleton get our bone mapping; others (Mixamo) are auto-mapped. The
            // mapping needs the skeleton's rest pose, which Unity only knows after a first import;
            // OnPostprocessModel records it and imports again.
            HumanDescription description = importer.humanDescription;
            if (!HasOurSkeleton(description.skeleton?.Select(b => b.name))) return;
            description.human = Map.Select(m => new HumanBone
            {
                boneName = m.bone,
                humanName = m.human,
                limit = new HumanLimit { useDefaultValues = true },
            }).ToArray();
            importer.humanDescription = description;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root)) return;
            if (!HasOurSkeleton(root.GetComponentsInChildren<Transform>(true).Select(t => t.name))) return;
            var importer = (ModelImporter)assetImporter;
            SkeletonBone[] skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale,
            }).ToArray();
            if (Same(importer.humanDescription.skeleton, skeleton)) return;

            // The first import, or the art's skeleton changed: record its rest pose (T-pose), then
            // import again so OnPreprocessModel can map the bones.
            HumanDescription description = importer.humanDescription;
            description.skeleton = skeleton;
            importer.humanDescription = description;
            string path = assetPath;
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            };
        }

        private static bool HasOurSkeleton(IEnumerable<string> names)
        {
            if (names == null) return false;
            var set = new HashSet<string>(names);
            return Map.All(m => set.Contains(m.bone));
        }

        private static bool Same(SkeletonBone[] a, SkeletonBone[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].name != b[i].name || (a[i].position - b[i].position).sqrMagnitude > 1e-8f ||
                    Quaternion.Angle(a[i].rotation, b[i].rotation) > 0.01f)
                    return false;
            return true;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Animations)) return;
            var importer = (ModelImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool loop = IsLooping(name);
            bool locomotion = name.StartsWith("walk") || name.StartsWith("run");

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                clip.loopTime = loop;
                clip.loopPose = loop;
                // Turning and bobbing stay in the pose; only locomotion keeps forward travel,
                // which the game reads (averageSpeed) to match stride to speed.
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = !locomotion;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
