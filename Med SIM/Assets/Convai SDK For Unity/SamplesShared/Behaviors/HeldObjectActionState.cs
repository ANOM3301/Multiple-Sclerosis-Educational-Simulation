using UnityEngine;

namespace Convai.Sample.Behaviors
{
    /// <summary>
    ///     Shared sample state for actions that pick up, hold, place, or drop scene objects.
    /// </summary>
    [AddComponentMenu("Convai/Samples/Held Object Action State")]
    public sealed class HeldObjectActionState : MonoBehaviour
    {
        [SerializeField] private Transform _attachPoint;
        [SerializeField] private GameObject _currentHeldObject;

        public Transform AttachPoint => _attachPoint;
        public GameObject CurrentHeldObject => ResolveCurrentHeldObject();
        public bool IsHolding => CurrentHeldObject != null;

        public void SetAttachPoint(Transform attachPoint)
        {
            _attachPoint = attachPoint;
            ResolveCurrentHeldObject();
        }

        public bool IsHoldingObject(GameObject target) =>
            target != null && CurrentHeldObject == target;

        public bool TryAttach(GameObject target, out string message)
        {
            message = string.Empty;
            if (target == null)
            {
                message = "No object available to hold.";
                return false;
            }

            if (_attachPoint == null)
            {
                message = "Pickup attach point not found.";
                return false;
            }

            GameObject held = CurrentHeldObject;
            if (held != null && held != target)
            {
                message = $"Already holding {held.name}.";
                return false;
            }

            _currentHeldObject = target;
            target.transform.SetParent(_attachPoint, worldPositionStays: false);
            target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = Quaternion.identity;
            return true;
        }

        public GameObject DetachHeldObject(bool worldPositionStays = true)
        {
            GameObject held = CurrentHeldObject;
            if (held == null)
                return null;

            held.transform.SetParent(null, worldPositionStays);
            _currentHeldObject = null;
            return held;
        }

        public void ClearIfHeld(GameObject target)
        {
            if (target == null)
                return;

            if (CurrentHeldObject != target)
                return;

            if (target.transform.parent == _attachPoint)
                target.transform.SetParent(null, worldPositionStays: true);

            _currentHeldObject = null;
        }

        private GameObject ResolveCurrentHeldObject()
        {
            if (_currentHeldObject != null)
                return _currentHeldObject;

            if (_attachPoint == null || _attachPoint.childCount == 0)
                return null;

            _currentHeldObject = _attachPoint.GetChild(_attachPoint.childCount - 1).gameObject;
            return _currentHeldObject;
        }
    }
}
