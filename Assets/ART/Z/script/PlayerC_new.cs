using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerC_new : MonoBehaviour
{
    public CharacterController controller;
    public float walkSpeed = 3;
    public Animator ani;
    public GameObject weaponObj;
    float ComboTimer;
    bool isControl = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }
    //if (Input.GetKey(KeyCode.W))
    //{
    //    Debug.Log("你按下了W键");
    //}
    // Update is called once per frame
    void Update()
    {  
        Atk();
        Move();
        ResetParams();
    }

    void Move()
    {
        Vector3 dir = transform.TransformDirection(new Vector3(Input.GetAxisRaw("Horizontal"),
            0,Input.GetAxisRaw("Vertical")));
        controller.SimpleMove(dir * walkSpeed);
        ani.SetFloat("前后速度" , Input.GetAxisRaw("Vertical"));
        ani.SetFloat("左右速度",  Input.GetAxisRaw("Horizontal")); 
        //controller.Move(dir * walkSpeed * Time.deltaTime); //Time.deltaTime 当前帧到上一帧的时间差值 
        // 设备1  帧率是30    一秒钟执行30次 update 30米  Time.deltaTime 1/30
        // 设备2  帧率是60    一秒钟执行60次 update 60米  Time.deltaTime 1/60 
        // Horizontal  A或左 -1  D或右 1 
        // Vertical    W或上 1   S或下 -1 
        //new Vector3(Input.GetAxisRaw("Horizontal"), 0,Input.GetAxisRaw("Vertical"))
        // 假如只按下了 W  
        // new Vector3(0,0,1)
        // 假如只按下了 S
        // new Vector3(0,0,-1)
        // 假如只按下了 A
        // new Vector3(-1,0,0)
        // 假如只按下了 D
        // new Vector3(1,0,0) 
        // W A
        // new Vector3(-1,0,1) 
        // transform.TransformDirection 
    }
    void Atk()
    {
        if (Input.GetMouseButtonDown(0) && isControl)
        {
            isControl = false;
            ComboTimer = 2;
            ani.SetTrigger("攻击触发"); 
            ani.SetInteger("连段" ,  (ani.GetInteger("连段") + 1) % 3 ); 
        }
    }
    public void  enableWeapon()
    {
        weaponObj.SetActive(true);
    }
    public void disableWeapon()
    {
        weaponObj.SetActive(false);
    }
    void ResetParams()
    {
        if ( ComboTimer <=0 && ani.GetInteger("连段") != 0)
        {
            ani.SetInteger("连段" , 0);
        }
        else
        {
            ComboTimer -= Time.deltaTime;
        }
    }
    public void resetControl()
    {
        isControl = true;
    }
}
