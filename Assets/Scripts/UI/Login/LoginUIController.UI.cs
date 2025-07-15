using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginUIController : MonoBehaviour
{
    public TMPro.TMP_InputField usernameInputField;
    public Button enterButton;
    public Button exitButton;
    public TMP_Text errorLabel;

    private void Awake()
    {
        enterButton.onClick.AddListener(OnEnterClicked);
        exitButton.onClick.AddListener(OnExitClicked);        
        errorLabel.text = ""; // limpiar al inicio
    }

    private void OnEnterClicked()
    {
        string username = usernameInputField.text.Trim();
        LoginManager.TryLogin(username, errorLabel);
    }

    private void OnExitClicked()
    {
        Application.Quit();
    }
}
