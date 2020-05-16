using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
    public Button button = null;

    private Animator animator = null;
    private bool dancing = true;

    void Start()
    {
        Application.targetFrameRate = 60;

        animator = GetComponent<Animator>();

        button.onClick.AddListener(OnClick);
    }
    
    private void OnClick()
    {
        dancing = !dancing;
        
        animator.SetBool("dancing", dancing);
    }
}
