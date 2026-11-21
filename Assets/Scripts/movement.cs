using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public class movement : MonoBehaviour
{
    public float speed;
    public float jumpPower;
    public bool CanJump = true;
    public Rigidbody2D rb;

    // Update is called once per frame
    void FixedUpdate()
    {
        float move = Input.GetAxis("Horizontal") * speed;
        rb.linearVelocity = new Vector2(move, rb.linearVelocity.y);

        if (CanJump && Input.GetKey(KeyCode.Space))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
            Debug.Log("Jumped!");
            CanJump = false;
        }

        if (gameObject.transform.position.y < -5)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.tag == "Ground")
        {
            CanJump = true;
        }
    }
}
