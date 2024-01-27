import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Canvas, Circle, Rect, RoundedRect, Skia, SkiaView, Text, interpolateColors, runTiming, useComputedValue, useFont, useValue, useValueEffect } from '@shopify/react-native-skia';
import React, { useCallback, useMemo } from 'react';
import { useEffect, useState } from 'react';
import { StyleSheet, View, useWindowDimensions } from 'react-native';
import 'react-native-url-polyfill/auto';

// Now import and use SignalR

interface TorrentAddedEvent {
  numberOfPieces: number
  name: string,
}

interface PieceDownloaded {
  pieceIndex: number
}

type Square = {
  x: number;
  y: number;
  size: number;
  pieceIndex: number
};

interface Piece {
  x: number;
  y: number;
  size: number;
  pieceIndex: number,
  bit: number
}

interface AnimatedSquareProps {
  x: number,
  y: number,
  size: number,
  bit: number
}

const AnimatedSquare = ({ x, y, size, bit }: AnimatedSquareProps) => {
  const progress = useValue(0);



  useEffect(() => {
    if (bit === 1) {
      console.log("BIT HIGH NOW");
      runTiming(progress, 1, { duration: 500 });
    }
  }, [bit]);


  const color2 = useComputedValue(() => {
    return interpolateColors(progress.current, [0, 1], ["#000a03", "#014a17"]);
  }, [progress]);

  return <Rect x={x} y={y} width={size} height={size} color={color2} />
}

export default function App() {

  const [bitfield, setBitfield] = useState([]);
  const { width, height } = useWindowDimensions();
  const [torrent, setTorrent] = useState<TorrentAddedEvent>();
  const [pieces, setPieces] = useState<Piece[]>([])
  const [connection, setConnection] = useState<HubConnection | null>(null);

  useEffect(() => {
    setConnection(new HubConnectionBuilder()
      .withUrl("https://55n382t8-5142.use.devtunnels.ms/torrents/hub")
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build());
  }, [])

  const divideCanvasIntoSquares = useCallback((n: number) => {
    const x = 0;
    const y = 0;
    const squareSize = Math.sqrt((width * height) / n);

    // Calculate how many squares can fit in one row and one column
    const squaresInRow = Math.floor(width / squareSize);
    const squaresInColumn = Math.floor(height / squareSize);

    // Adjust the number of squares in a row if it exceeds the desired total
    const totalSquares = squaresInRow * squaresInColumn;
    let adjustedSquaresInRow = squaresInRow;
    if (totalSquares > n) {
      adjustedSquaresInRow = Math.ceil(n / squaresInColumn);
    }

    let squares: Square[] = [];

    for (let row = 0; row < squaresInColumn; row++) {
      for (let col = 0; col < adjustedSquaresInRow; col++) {
        squares.push({
          x: x + col * squareSize,
          y: y + row * squareSize,
          size: squareSize,
          pieceIndex: (row * adjustedSquaresInRow) + col
        });
      }
    }

    return squares;
  }, [width, height])

  useEffect(() => {
    if (connection === null || !setTorrent) return;

    if (connection.state === HubConnectionState.Disconnected)
      connection.start().then(f => {
        console.log(connection.connectionId);
        console.log("WIRED IT UP");
        connection.on("TorrentAdded", data => {
          console.log({ data })
          setTorrent(data);
        })
      });


    () => {
      connection.stop();
    }
  }, [connection, setTorrent])

  const titleFont = useFont(require("./assets/fonts/Roboto-Light.ttf"), 13, err => {
    console.log({
      message: "THIS FONTS A GONER",
      err
    })
  });

  const torrentInfo = useMemo(() => {
    if (!torrent) {
      console.log("NO TORRENT");
      return (<></>)

    }

    if (titleFont === null) {
      console.log("NO FONT")
      return (<></>)
    }

    console.log("RUNNING?")
    const paddingW = width * 0.04;
    return (
      <>
        <Text font={titleFont} text={torrent.name} x={paddingW} y={100} color={"#dedede"} />
        <RoundedRect x={paddingW} y={114} width={width - (2 * paddingW)} height={4} color={"#1c1c1c"} r={4} />
      </>)
  }, [torrent, titleFont, width])


  return (
    <SkiaView style={styles.container}>
      <Canvas style={{ position: 'absolute', width: "100%", height: "100%" }} pointerEvents="none">
        <Circle cx={200} cy={200} r={30} color={"white"} />
        {torrentInfo}
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
