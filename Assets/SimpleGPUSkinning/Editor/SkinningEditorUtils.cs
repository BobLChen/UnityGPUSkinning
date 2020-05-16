using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

class SkinBone
{
    public int depth  = -1;
    public int parent = -1;
    public int index  = -1;
    public Transform transform = null;
}

public class SkinningEditorUtils
{
    [MenuItem("Assets/GPUSkinning/GenSkinMesh")]
    private static void GenerateSkinMesh()
    {
        GameObject asset = Selection.activeGameObject;
        if (asset == null)
        {
            Debug.LogError("Selection must be a fbx file.");
            return;
        }

        string file = AssetDatabase.GetAssetPath(asset);
        if (Path.GetExtension(file).ToLower() != ".fbx")
        {
            Debug.LogError("Selection must be a fbx file.");
            return;
        }
        
        var importer = AssetImporter.GetAtPath(file) as ModelImporter;
        if (importer.optimizeGameObjects)
        {
            Debug.LogError("Optimize Game Objects muse unselect.");
            return;
        }
        
        List<SkinBone> bones = GenerateBones(file);
        
        GenerateAvatarData(file, bones);
        
        GenerateMeshData(bones, file);

        GenerateAnimData(bones, file);
    }

    private static void GenerateAnimData(List<SkinBone> bones, string file)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(file);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip)
            {
                AnimationClip clip = asset as AnimationClip;
                GenerateClip(bones, clip, file);
            }
        }
    }

    private static void GenerateClip(List<SkinBone> bones, AnimationClip clip, string file)
    {
        EditorCurveBinding[] curveDatas = AnimationUtility.GetCurveBindings(clip);
        
        float frequency = 1.0f / 30.0f;
        int keyframeLen = (int)Mathf.Ceil(clip.length / frequency) + 1;
        List<float> times = new List<float>();
        for (int i = 0; i < keyframeLen; ++i) {
            times.Add(i * frequency >= clip.length ? clip.length : i * frequency);
        }
        
        SkinAnimationClip newClip = ScriptableObject.CreateInstance<SkinAnimationClip>();
        newClip.frequency = frequency;
        newClip.times     = times.ToArray();
        newClip.datas     = new float[bones.Count * keyframeLen * SkinAnimationClip.STRIDE];
        newClip.length    = clip.length;
        newClip.animName  = clip.name;
        newClip.nodeCount = bones.Count;
        
        Dictionary<string, int> boneIndexMap = new Dictionary<string, int>();
        for (int i = 0; i < bones.Count; ++i)
        {
            SkinBone bone  = bones[i];
            Vector3 pos    = bone.transform.localPosition;
            Quaternion rot = bone.transform.localRotation;

            int frameIndex = i * keyframeLen * SkinAnimationClip.STRIDE;
            for (int j = 0; j < keyframeLen; ++j)
            {
                int curveIndex = j * SkinAnimationClip.STRIDE;
                newClip.datas[frameIndex + curveIndex + 0] = pos.x;
                newClip.datas[frameIndex + curveIndex + 1] = pos.y;
                newClip.datas[frameIndex + curveIndex + 2] = pos.z;

                newClip.datas[frameIndex + curveIndex + 3] = rot.x;
                newClip.datas[frameIndex + curveIndex + 4] = rot.y;
                newClip.datas[frameIndex + curveIndex + 5] = rot.z;
                newClip.datas[frameIndex + curveIndex + 6] = rot.w;
            }
            
            boneIndexMap[bone.transform.name] = i;
        }
        
        float[] values = new float[keyframeLen];
        
        for (int i = 0; i < curveDatas.Length; ++i)
        {
            EditorCurveBinding binding = curveDatas[i];
            string name = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            
            int offset = -1;
            switch (binding.propertyName)
            {
                case "m_LocalPosition.x": offset = 0; break;
                case "m_LocalPosition.y": offset = 1; break;
                case "m_LocalPosition.z": offset = 2; break;

                case "m_LocalRotation.x": offset = 3; break;
                case "m_LocalRotation.y": offset = 4; break;
                case "m_LocalRotation.z": offset = 5; break;
                case "m_LocalRotation.w": offset = 6; break;
            }
            
            if (offset == -1) {
                continue;
            }
            
            if (!boneIndexMap.ContainsKey(name)) {
                continue;
            }
            
            for (int j = 0; j < keyframeLen; ++j) {
                values[j] = curve.Evaluate(times[j]);
            }

            int frameIndex = boneIndexMap[name] * keyframeLen * SkinAnimationClip.STRIDE;
            for (int j = 0; j < keyframeLen; ++j) {
                int curveIndex = j * SkinAnimationClip.STRIDE;
                newClip.datas[frameIndex + curveIndex + offset] = values[j];
            }
        }
        
        string basePath = Path.GetDirectoryName(file) + "/" + Path.GetFileNameWithoutExtension(file);
        
        string clipPath = basePath + clip.name + ".anim.asset";
        AssetDatabase.CreateAsset(newClip, clipPath);
        
        AnimationClip emptyClip = new AnimationClip();
        AnimationCurve emptyCurve = new AnimationCurve();
        emptyCurve.AddKey(new Keyframe(0.0f, 0.0f));
        emptyCurve.AddKey(new Keyframe(clip.length, clip.length));
        emptyClip.SetCurve("", typeof(SkinFloat), "value", emptyCurve);
        AssetDatabase.CreateAsset(emptyClip, basePath + clip.name + ".anim");
    }
    
    private static void GenerateMeshData(List<SkinBone> totalBones, string file)
    {
        string basePath = Path.GetDirectoryName(file) + "/" + Path.GetFileNameWithoutExtension(file);

        Dictionary<string, SkinBone> bonesDict = new Dictionary<string, SkinBone>();
        for (int i = 0; i < totalBones.Count; ++i) {
            bonesDict[totalBones[i].transform.name] = totalBones[i];
        }
        
        GameObject gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        foreach (var skinRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh oriMesh = skinRenderer.sharedMesh;
            Mesh newMesh = new Mesh();
            
            Transform[] refBones  = skinRenderer.bones;
            Matrix4x4[] bindPose  = oriMesh.bindposes;
            Vector3[] vertices    = oriMesh.vertices;
            Vector3[] normals     = oriMesh.normals;
            BoneWeight[] weights  = oriMesh.boneWeights;
            
            Vector3[] newVertices = new Vector3[oriMesh.vertexCount];
            Vector3[] newNormals  = new Vector3[oriMesh.vertexCount];
            Vector2[] boneIndices = new Vector2[oriMesh.vertexCount];
            Vector2[] boneWeights = new Vector2[oriMesh.vertexCount];
            
            for (int i = 0; i < oriMesh.vertexCount; ++i)
            {
                Vector3 vertex = vertices[i];
                Vector3 normal = normals[i];
                BoneWeight weight = weights[i];
                
                float weightTotal = weight.weight0 + weight.weight1;
                float boneWeight0 = weight.weight0 / weightTotal;
                float boneWeight1 = weight.weight1 / weightTotal;

                int boneIndex0 = bonesDict[refBones[weight.boneIndex0].name].index;
                int boneIndex1 = bonesDict[refBones[weight.boneIndex1].name].index;
                int boneIndex2 = bonesDict[refBones[weight.boneIndex2].name].index;
                int boneIndex3 = bonesDict[refBones[weight.boneIndex3].name].index;
                
                boneIndices[i].x = boneIndex0;
                boneIndices[i].y = boneIndex1;
                boneWeights[i].x = boneWeight0;
                boneWeights[i].y = boneWeight1;
                
                Matrix4x4 pose0 = refBones[weight.boneIndex0].localToWorldMatrix * bindPose[weight.boneIndex0];
                Matrix4x4 pose1 = refBones[weight.boneIndex1].localToWorldMatrix * bindPose[weight.boneIndex1];
                Matrix4x4 pose2 = refBones[weight.boneIndex2].localToWorldMatrix * bindPose[weight.boneIndex2];
                Matrix4x4 pose3 = refBones[weight.boneIndex3].localToWorldMatrix * bindPose[weight.boneIndex3];
                
                Vector3 v0 = pose0.MultiplyPoint(vertex) * weight.weight0;
                Vector3 v1 = pose1.MultiplyPoint(vertex) * weight.weight1;
                Vector3 v2 = pose2.MultiplyPoint(vertex) * weight.weight2;
                Vector3 v3 = pose3.MultiplyPoint(vertex) * weight.weight3;
                newVertices[i] = v0 + v1 + v2 + v3;
                
                Vector3 n0 = pose0.MultiplyVector(normal) * weight.weight0;
                Vector3 n1 = pose1.MultiplyVector(normal) * weight.weight1;
                Vector3 n2 = pose2.MultiplyVector(normal) * weight.weight2;
                Vector3 n3 = pose3.MultiplyVector(normal) * weight.weight3;
                newNormals[i] = (n0 + n1 + n2 + n3).normalized;
            }
            
            newMesh.name         = oriMesh.name;
            newMesh.vertices     = newVertices;
            newMesh.subMeshCount = oriMesh.subMeshCount;
            newMesh.triangles    = oriMesh.triangles;
            newMesh.normals      = newNormals;
            newMesh.uv           = oriMesh.uv;
            newMesh.uv2          = boneIndices; // TEXCOORD1
            newMesh.uv3          = boneWeights; // TEXCOORD2
            
            ReCalculateTangent(newMesh);
            
            AssetDatabase.CreateAsset(newMesh, basePath + "_" + oriMesh.name + ".asset");
        }
    }

    private static void GenerateAvatarData(string file, List<SkinBone> bones)
    {
        string basePath = Path.GetDirectoryName(file) + "/" + Path.GetFileNameWithoutExtension(file);

        SkinAvatar skinAvatar  = ScriptableObject.CreateInstance<SkinAvatar>();
        skinAvatar.bonesParent = new int[bones.Count];
        skinAvatar.bonesName   = new string[bones.Count];
        skinAvatar.invBindPose = new float[bones.Count * 7];

        for (int i = 0; i < bones.Count; ++i)
        {
            skinAvatar.bonesParent[i] = bones[i].parent;
            skinAvatar.bonesName[i]   = bones[i].transform.name;

            var inv = bones[i].transform.worldToLocalMatrix;
            var pos = inv.GetColumn(3);
            var rot = Quaternion.LookRotation(inv.GetColumn(2), inv.GetColumn(1));
            
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 0] = pos.x;
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 1] = pos.y;
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 2] = pos.z;

            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 3] = rot.x;
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 4] = rot.y;
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 5] = rot.z;
            skinAvatar.invBindPose[i * SkinAnimationClip.STRIDE + 6] = rot.w;
        }

        AssetDatabase.CreateAsset(skinAvatar,  basePath + "_SkeletonAvatar.asset");
        
        GameObject gameObject = new GameObject();
        gameObject.name = "Empty";
        Avatar avatar = AvatarBuilder.BuildGenericAvatar(gameObject, "Empty");
        AssetDatabase.CreateAsset(avatar, basePath + "_Empty.avatar.asset");
        GameObject.DestroyImmediate(gameObject);
    }
    
    private static List<SkinBone> GenerateBones(string fbxfile)
    {
        GameObject gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(fbxfile);

        List<SkinBone> bones = new List<SkinBone>();
        
        RecursiveBones(gameObject.transform, bones, 0);
        
        bones.Sort((SkinBone a, SkinBone b) => {
            if (a.depth < b.depth) {
                return -1;
            }    
            else if (a.depth == b.depth) {
                return 0;
            }
            else {
                return 1;
            }
        });
        
        Dictionary<string, int> indexDict = new Dictionary<string, int>();
        for (int i = 0; i < bones.Count; ++i)
        {
            SkinBone bone = bones[i];
            indexDict[bone.transform.name] = i;

            bone.index = i;

            int parentIndex = -1;
            if (bone.transform.parent != null && indexDict.TryGetValue(bone.transform.parent.name, out parentIndex)) {
                bone.parent = parentIndex;
            }
            else {
                bone.parent = -1;
            }
        }
        
        return bones;
    }
    
    private static void RecursiveBones(Transform root, List<SkinBone> bones, int depth)
    {
        for (int i = 0; i < root.childCount; ++i)
        {
            Transform transform = root.GetChild(i);

            bool isBone = true;
            foreach (var comp in transform.GetComponents<Component>()) 
            {
                if (!(comp is Transform)) {
                    isBone = false;
                    break;
                }
            }

            if (isBone)
            {
                SkinBone bone = new SkinBone();
                bone.depth = depth;
                bone.transform = transform;
                bones.Add(bone);
                RecursiveBones(transform, bones, depth + 1);
            }
        }
    }
    
    public static void ReCalculateTangent(Mesh mesh)
    {
        Vector3[] vertices  = mesh.vertices;
        Vector3[] normals   = mesh.normals;
        Vector2[] texcoords = mesh.uv;
        int[] triangles     = mesh.triangles;
        int vertexCount     = vertices.Length;
        int triangleCount   = triangles.Length / 3;

        Vector4[] tangents = new Vector4[vertexCount];
        Vector3[] tan1 = new Vector3[vertexCount];
        Vector3[] tan2 = new Vector3[vertexCount];

        int tri = 0;
        for (int i = 0; i <= (triangleCount - 1); ++i)
        {
            int i1 = triangles[tri + 0];
            int i2 = triangles[tri + 1];
            int i3 = triangles[tri + 2];

            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];
            Vector3 v3 = vertices[i3];

            Vector2 w1 = texcoords[i1];
            Vector2 w2 = texcoords[i2];
            Vector2 w3 = texcoords[i3];

            float x1 = v2.x - v1.x;
            float x2 = v3.x - v1.x;
            float y1 = v2.y - v1.y;
            float y2 = v3.y - v1.y;
            float z1 = v2.z - v1.z;
            float z2 = v3.z - v1.z;

            float s1 = w2.x - w1.x;
            float s2 = w3.x - w1.x;
            float t1 = w2.y - w1.y;
            float t2 = w3.y - w1.y;

            float r = 1.0f / (s1 * t2 - s2 * t1);
            if (s1 * t2 == s2 * t1) {
                r = 1;
            }
            
            Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
            Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

            tan1[i1] += sdir;
            tan1[i2] += sdir;
            tan1[i3] += sdir;

            tan2[i1] += tdir;
            tan2[i2] += tdir;
            tan2[i3] += tdir;

            tri += 3;
        }

        for (int i = 0; i <= (vertexCount - 1); ++i)
        {
            Vector3 n = normals[i];
            Vector3 t = tan1[i];

            Vector3.OrthoNormalize(ref n, ref t);

            tangents[i].x = t.x;
            tangents[i].y = t.y;
            tangents[i].z = t.z;
            
            int tW = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0) ? -1 : 1;
            tangents[i].w = tW;
        }

        mesh.tangents = tangents;
    }
}