using UnityEngine;
// Get access to UI toolkit
using UnityEngine.UIElements;

public class CustomBottonEvent : MonoBehaviour
{
    public Renderer rend;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // UI 계층의 최상위 요소 VisualElement 가져오기
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        // Button 가져오기
        Button button1 = root.Q<Button>("Button1");
        Button button2 = root.Q<Button>("Button2");
        Button button3 = root.Q<Button>("Button3");

        // Button 클릭시 실행할 함수 동작
        button1.clicked += () => SetColor(Color.red);
        button2.clicked += () => SetColor(Color.blue);
        button3.clicked += () => SetColor(Color.white);

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetColor(Color _color)
    {
        rend.material.color= _color;
    }
}
