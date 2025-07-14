
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LoginManager
{
  public static void TryLogin(string username, TMPro.TMP_Text errorLabel)
  {
    if (string.IsNullOrWhiteSpace(username))
    {
      errorLabel.text = "Please enter a valid username.";
      return;
    }
    var data = LoadSystem.LoadPlayer();
    if (data != null && data.playerName == username)
    {
      // Usuario existe, cargar datos
      SessionData.SetUsername(username);
      Debug.Log($"Login successful for {username} (existing user)");
      SceneManager.LoadSceneAsync("AvatarCreator");
      return;
    }
    else
    {
      errorLabel.text = "User not found. Please create a new account.";
      Debug.LogWarning($"Login failed for {username} (user not found)");
      // Aquí podrías redirigir a una pantalla de registro si es necesario
      return;

    }
  }
}

public static class SessionData
{
  public static string Username { get; private set; }
  public static void SetUsername(string username)
  {
    Username = username;
  }
}
