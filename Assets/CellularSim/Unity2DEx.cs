using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace CellularSim {
    public static class Unity2DEx {
         public static bool TryGetMousePosition(this RectTransform rectTransform, out Vector2 position) {
             Vector2 size = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);
             var rect= new Rect((Vector2)rectTransform.position - (size * 0.5f), size);
             Vector2 mouse = Input.mousePosition;
             if (rect.Contains(mouse)) {
                 position= (mouse - rect.min)/rect.size;
                 return true;
             }
             position = default;
             return false;
         }
        public static bool TryGetMousePosition(this RectTransform rectTransform, out Vector2 position,out Rect rect) {
             Vector2 size = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);
             rect= new Rect((Vector2)rectTransform.position - (size * 0.5f), size);
             Vector2 mouse = Input.mousePosition;
             if (rect.Contains(mouse)) {
                 position= (mouse - rect.min)/rect.size;
                 return true;
             }
             position = default;
             return false;
         } public static bool TryGetMousePosition(this RectTransform rectTransform,Camera camera, out Vector2 position,out Rect rect) {
            rect= RectTransformToScreenSpace(rectTransform,camera);
             Vector2 mouse = Input.mousePosition;
             if (rect.Contains(mouse)) {
                 position= (mouse - rect.min)/rect.size;
                 return true;
             }
             position = default;
             return false;
         }
        public static void GetLocalCorners(this RectTransform transform,Span<Vector3> fourCornersArray)
        {
            if (fourCornersArray == null || fourCornersArray.Length < 4)
            {
                Debug.LogError((object) "Calling GetLocalCorners with an array that is null or has less than 4 elements.");
            }
            else
            {
                Rect rect = transform.rect;
                var x = rect.x;
                var y = rect.y;
                var xMax = rect.xMax;
                var yMax = rect.yMax;
                fourCornersArray[0] = new Vector3(x, y, 0.0f);
                fourCornersArray[1] = new Vector3(x, yMax, 0.0f);
                fourCornersArray[2] = new Vector3(xMax, yMax, 0.0f);
                fourCornersArray[3] = new Vector3(xMax, y, 0.0f);
            }
        }
        public static void GetWorldCorners(this RectTransform transform ,Span<Vector3> fourCornersArray)
        {
            if (fourCornersArray == null || fourCornersArray.Length < 4)
            {
                Debug.LogError((object) "Calling GetWorldCorners with an array that is null or has less than 4 elements.");
            }
            else
            {
                transform.GetLocalCorners(fourCornersArray);
                Matrix4x4 localToWorldMatrix = transform.transform.localToWorldMatrix;
                for (int index = 0; index < 4; ++index)
                    fourCornersArray[index] = localToWorldMatrix.MultiplyPoint(fourCornersArray[index]);
            }
        }
        public static Rect RectTransformToScreenSpace(RectTransform transform, Camera cam)
        {
            Span<Vector3> worldCorners = stackalloc Vector3[4];
            Span<Vector3>  screenCorners = stackalloc Vector3[4];
 
            transform.GetWorldCorners(worldCorners);
 
            for (int i = 0; i < 4; i++)
            {
                screenCorners[i] = cam.WorldToScreenPoint(worldCorners[i]);
            }
 
            return new Rect(screenCorners[0].x,
                screenCorners[0].y,
                screenCorners[2].x - screenCorners[0].x,
                screenCorners[2].y - screenCorners[0].y);
        }
         public static bool TryGetMouseButtonDownPosition(this Camera camera,int mouse, out Vector2 position) {
             if (Input.GetMouseButtonDown(mouse)) {

                 var mousePosition = Input.mousePosition;
                 mousePosition.z = -10;
                 position=(Vector2) camera.ScreenToWorldPoint(mousePosition);
                 return true;
             }
             position = default;
             return false;
         }
public static bool TryGetMouseButtonPosition(this Camera camera,int mouse, out Vector2 position) {
             if (Input.GetMouseButton(mouse)) {

                 var mousePosition = Input.mousePosition;
                 mousePosition.z = -10;
                 position=(Vector2) camera.ScreenToWorldPoint(mousePosition);
                 return true;
             }
             position = default;
             return false;
         }
    }
}