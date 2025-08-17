
using System;
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
      if (data == null)
      {
        errorLabel.text = "No se pudo cargar el perfil del usuario.";
        return;
      }
      PlayerSessionService.SetPlayer(data);
      if (data.heroes.Count == 0)
      {
        // Si no hay héroes, redirigir a la pantalla de creación de héroe
        SceneManager.LoadSceneAsync("AvatarCreator");
        return;
      }
      SceneManager.LoadSceneAsync("CharacterSelecctionScene");
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
