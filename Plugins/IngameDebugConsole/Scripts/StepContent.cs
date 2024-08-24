using UnityEngine;
using UnityEngine.UI;

public class StepContent : MonoBehaviour
{
    [SerializeField]
    private Text stepNOText;
    [SerializeField]
    private InputField stepContentInputField;

    [HideInInspector]
    public string StepContentDetail;

    public void SetStep( int stepNO, string content )
    {
        stepNOText.text = stepNO.ToString();
        stepContentInputField.text = content;
    }

    public void OnValueChange()
    {
        StepContentDetail = stepContentInputField.text;
    }
}
