using UnityEngine;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private RectTransform healthFill; // The colored fill
    [SerializeField, Range(0f, 1f)] private float currentHealth = 325f;

    private float originalWidth;

    private void Awake()
    {
        if (healthFill == null)
        {
            Debug.LogError("Assign the healthFill RectTransform in inspector!");
            return;
        }
        originalWidth = healthFill.sizeDelta.x;
        UpdateHealthBar();
    }

    public void TakeDamage(float damage)
    {
        SetHealth(currentHealth - damage);
    }

    public void SetHealth(float normalizedHealth)
    {
        currentHealth = Mathf.Clamp01(normalizedHealth);
        UpdateHealthBar();
    }

    public float GetHealth()
    {
        return currentHealth;
    }

    private void UpdateHealthBar()
    {
        Vector2 size = healthFill.sizeDelta;
        size.x = originalWidth * currentHealth;
        healthFill.sizeDelta = size;
    }
}
