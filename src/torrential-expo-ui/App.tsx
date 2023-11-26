import { Canvas, Circle, SkiaView } from '@shopify/react-native-skia';
import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';

export default function App() {
  return (
    <SkiaView style={styles.container}>
      <Canvas style={{ position: 'absolute', width: "100%", height: "100%" }} pointerEvents="none" key={"nav-fab"}>
        <Circle cx={100} cy={100} r={30} color={"white"} />
      </Canvas>
    </SkiaView>

  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: 'black',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
