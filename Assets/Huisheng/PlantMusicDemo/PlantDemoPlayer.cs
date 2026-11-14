using UnityEngine;
using UnityEngine.InputSystem;
namespace Huisheng.PlantMusic
{
    [RequireComponent(typeof(Rigidbody2D),typeof(CircleCollider2D))]
    public class PlantDemoPlayer : MonoBehaviour
    {
        public float speed=4;
        Rigidbody2D body;
        Vector2 move;
        void Awake()
        {
            body=GetComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;
            body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            body.interpolation=RigidbodyInterpolation2D.Interpolate;
            GetComponent<CircleCollider2D>().radius=.22f;
        }
        void Update()
        {
            var k=Keyboard.current;move=Vector2.zero;if(k==null)return;
            move.x=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
            move.y=(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0);
            move=move.normalized;
        }
        void FixedUpdate()
        {
            var p=body.position+move*speed*Time.fixedDeltaTime;
            p.x=Mathf.Clamp(p.x,-8.1f,8.1f);p.y=Mathf.Clamp(p.y,-3.8f,1.1f);
            body.MovePosition(p);
        }
        public void ResetPosition(){body.position=new Vector2(0,-3.3f);body.linearVelocity=Vector2.zero;}
    }
}
