import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Box, CloseButton, Text, ToastId, useToast } from "@chakra-ui/react";
import {
  ToastNotificationPayload,
  dequeueNext,
} from "../../store/slices/notificationsSlice";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import classes from "./toast-notification.module.css";
import { type IconDefinition } from "@fortawesome/free-solid-svg-icons";
import { RootState } from "../../store";

export const selectCurrentToast = (state: RootState) =>
  state.notifications.currentToast;

const renderToast = (
  id: ToastId,
  onClose: () => void,
  notification: ToastNotificationPayload
) => {
  const { status, icon, title, description } = notification;
  let bgColor = "blue.300";
  if (status === "success") bgColor = "green.300";
  if (status === "warning") bgColor = "orange.300";
  if (status === "error") bgColor = "red.300";
  return (
    <Box bg={bgColor} id={id.toString()} className={classes.toastContainer}>
      <Box className={classes.icon}>
        {typeof icon !== "string" && icon !== undefined && (
          <FontAwesomeIcon icon={icon as IconDefinition} size={"xl"} />
        )}
      </Box>
      <Box className={classes.content}>
        <Box className={classes.title}>
          <Text size={"sm"}>{title}</Text>
        </Box>
        <Box className={classes.description}>
          <Text size={"sm"}>{description}</Text>
        </Box>
      </Box>
      <Box className={classes.close}>
        <CloseButton onClick={onClose} />
      </Box>
    </Box>
  );
};

export default function ToastNotification() {
  const dispatch = useDispatch();
  const toast = useToast();
  const currentToast = useSelector(selectCurrentToast);

  useEffect(() => {
    dispatch(dequeueNext());
  }, [dispatch]);

  useEffect(() => {
    if (currentToast) {
      const { duration } = currentToast;

      toast({
        position: "bottom-right",
        duration,
        render: ({ id, onClose }) => renderToast(id!, onClose, currentToast),
      });

      dispatch(dequeueNext());
    }
  }, [currentToast, toast, dispatch]);

  return <></>;
}
