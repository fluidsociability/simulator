/*
FLUID Sociability - a simulation tool for evaluating building designs
Copyright (C) 2022 Human Studio, Inc.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using FLUID;
using UnityEngine;

public class Orbit : MonoBehaviour
{
    private Vector3 _previousMousePosition;
    private float _previousYawInDegrees;
    private float _previousTiltInDegrees;

    public Transform logos;

    private void Update()
    {
        Transform orbitTransform = transform.parent.parent;
        Transform tiltTransform = transform.parent;
        float scale = transform.localPosition.z / Screen.width;

        float _scrollStepDistance = 20;
        //StructuredCausalModel structuredCausalModel = logos.GetComponent<StructuredCausalModel>();
        //MeshFilter navigationMeshFilter = logos.GetComponent<MeshFilter>();
        //if (navigationMeshFilter.mesh.triangles.Length > 0)
        //{
        //    _scrollStepDistance = navigationMeshFilter.mesh.bounds.extents.magnitude / 3;
        //}

        if (Input.GetKey(KeyCode.Mouse0))
        {
            #region Orbit
            const float _turnSpeed = 7;
            Vector3 distanceMoved = (Input.mousePosition - _previousMousePosition) * scale;
            orbitTransform.localRotation = Quaternion.Euler(
                0,
                _previousYawInDegrees - _turnSpeed * distanceMoved.x,
                0
            );
            tiltTransform.localRotation = Quaternion.Euler(
                Mathf.Max(
                    -90,
                    Mathf.Min(90, _previousTiltInDegrees + _turnSpeed * distanceMoved.y)
                ),
                0,
                0
            );
            #endregion
        }
        if (Input.GetKey(KeyCode.Mouse1))
        {
            Vector3 distanceMoved = Input.mousePosition - _previousMousePosition;

            Vector3 yawedDistanceMoved =
                Quaternion.AngleAxis(_previousYawInDegrees, Vector3.up) * distanceMoved;
            Vector3 tiltedAndYawedDistanceMoved =
                Quaternion.AngleAxis(_previousTiltInDegrees, Vector3.right) * yawedDistanceMoved;

            orbitTransform.localPosition += tiltedAndYawedDistanceMoved * scale;
        }

        var mouseScrollWheel = Input.GetAxis("Mouse ScrollWheel");
        orbitTransform.localPosition += new Vector3(
            mouseScrollWheel
                * _scrollStepDistance
                * Mathf.Sin(Mathf.Deg2Rad * orbitTransform.localEulerAngles.y),
            -mouseScrollWheel
                * _scrollStepDistance
                * Mathf.Sin(Mathf.Deg2Rad * tiltTransform.localEulerAngles.x),
            mouseScrollWheel
                * _scrollStepDistance
                * Mathf.Cos(Mathf.Deg2Rad * tiltTransform.localEulerAngles.x)
                * Mathf.Cos(Mathf.Deg2Rad * orbitTransform.localEulerAngles.y)
        );

        _previousYawInDegrees = orbitTransform.localEulerAngles.y;
        _previousTiltInDegrees =
            tiltTransform.localEulerAngles.x > 90
                ? tiltTransform.localEulerAngles.x - 360
                : tiltTransform.localEulerAngles.x;
        _previousMousePosition = Input.mousePosition;
    }

    public void ResetCamera()
    {
        if (logos != null)
        {
            Transform orbitTransform = transform.parent.parent;
            Transform tiltTransform = transform.parent;

            Vector3 min;
            Vector3 max;
            StructuredCausalModel.FitViewToModel(logos, out min, out max);
            orbitTransform.position = (min + max) / 2;

            tiltTransform.rotation = Quaternion.Euler(45, 0, 0);

            float height = max.y - min.y;
            float depth = max.z - min.z;
            float offset = Math.Max(height, depth);
            transform.localPosition = new Vector3(0, 0, -offset);
        }
    }
}
