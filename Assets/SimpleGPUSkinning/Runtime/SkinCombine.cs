using UnityEngine;

public class SkinCombine : MonoBehaviour
{
    private SkinAnimation m_SkinAnimation = null;
    private MeshRenderer m_MeshRenderer = null;

    private void Start()
    {
        m_MeshRenderer  = GetComponent<MeshRenderer>();
        m_SkinAnimation = GetSkinAnimation();
        m_SkinAnimation.OnPoseBufferUpdate += OnAnimationUpdate;
    }

    private void OnAnimationUpdate()
    {
        m_MeshRenderer.SetPropertyBlock(m_SkinAnimation.poseBuffer);
    }
    
    private SkinAnimation GetSkinAnimation()
    {
        Transform node = transform;

        int depth = 5;
        while (depth > 0)
        {
            Transform parent = node.parent;
            SkinAnimation animation = parent.GetComponent<SkinAnimation>();
            if (animation != null) {
                return animation;
            }

            node  = node.parent;
            depth -= 1;
        }

        return null;
    }
}
