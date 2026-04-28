using UnityEngine;

public class bounce : MonoBehaviour
{
    public float bounceForce = 15f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Prefer player-controlled bounce logic (so we can check landing angle).
        var pc = collision.gameObject.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.HandleTrampolineBounce(bounceForce);
            return;
        }

        // Fallback: bounce anything with a Rigidbody2D.
        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
    }
}
