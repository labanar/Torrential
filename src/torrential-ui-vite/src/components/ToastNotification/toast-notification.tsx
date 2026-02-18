import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import {
  ToastNotificationPayload,
  dequeueNext,
} from "../../store/slices/notificationsSlice";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import classes from "./toast-notification.module.css";
import { type IconDefinition } from "@fortawesome/free-solid-svg-icons";
import { RootState } from "../../store";
import { toast } from "sonner";
import { X } from "lucide-react";
import classNames from "classnames";

export const selectCurrentToast = (state: RootState) =>
  state.notifications.currentToast;

const renderToast = (
  id: string | number,
  notification: ToastNotificationPayload
) => {
  const { status, icon, title, description, isClosable } = notification;

  return (
    <div
      id={id.toString()}
      className={classNames(classes.toastContainer, classes[status])}
    >
      <div className={classes.icon}>
        {typeof icon !== "string" && icon !== undefined && (
          <FontAwesomeIcon icon={icon as IconDefinition} size={"xl"} />
        )}
      </div>
      <div className={classes.content}>
        <div className={classes.title}>{title}</div>
        <div className={classes.description}>{description}</div>
      </div>
      {isClosable && (
        <div className={classes.close}>
          <button
            type="button"
            className={classes.closeButton}
            onClick={() => toast.dismiss(id)}
            aria-label="Close notification"
          >
            <X size={14} />
          </button>
        </div>
      )}
    </div>
  );
};

export default function ToastNotification() {
  const dispatch = useDispatch();
  const currentToast = useSelector(selectCurrentToast);

  useEffect(() => {
    dispatch(dequeueNext());
  }, [dispatch]);

  useEffect(() => {
    if (currentToast) {
      toast.custom((id) => renderToast(id, currentToast), {
        duration: currentToast.duration,
      });

      dispatch(dequeueNext());
    }
  }, [currentToast, dispatch]);

  return null;
}
