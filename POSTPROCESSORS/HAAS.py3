#!/usr/bin/python3
# coding=cp1251
"""
---------------- HAAS ------------------------

VERSION 18

FILE_EXTENSION  nc


BEGIN_MACHINE_CYCLES

ALPHABET( X, Y, Z, U, V, W, R, I, J, K, Q, T, H, S, F, M, G );

FORMAT( print=1, number=1, format_number=N%03f, %m=%5.3m, %n=%5.3n, %f=%5.3f );

CODE( 'Head' ); FORMAT( number=0 );
                PRINT( '%' );
                PRINT( 'O00001' );
                FORMAT( number=1 );

CODE( 'Safe' ); PRINT( 'G90 G40 G17' );
CODE( 'Comm' ); PRINT( '' );

CODE( 'BegOper' ); PRINT( '' );
CODE( 'EndOper' ); PRINT( '' );

CODE( 'T' ); FORMAT( number=0 );
             PRINT( '( ------------------ )' );
             PRINT( '( T%1f   Tool D%3.2n )', _T, _DIAM );
             PRINT( '( ------------------ )' );
             FORMAT( number=1, print=_T!=_T0 );
             PRINT( 'T%02f M06', _T );
             PRINT( 'G54 G90 X0 Y0' );
             PRINT( 'G43 H%2f Z%n', _T, _ZTOP );
             FORMAT( print=1 );
             PRINT( 'S%n F%n M%1n', _SPIN, _FEED, _SDIR );

CODE( 'S' );  PRINT( 'S%m', _S );
CODE( 'F' );  RRINT( 'F%m', _F );
CODE( 'SF' ); PRINT( 'S%m F%m', _S, _F );

CODE( 'G00' ); PRINT( 'G%1m X%m Y%m Z%m', 0, _X2, _Y2, _Z2 );
CODE( 'G01' ); PRINT( 'G%1m X%m Y%m Z%m F%m', 1, _X2, _Y2, _Z2, _F );
CODE( 'G//' ); PRINT( 'G%1m X%m Y%m Z%m R%n F%m', 2, _X2, _Y2, _Z2, _R, _F );
CODE( 'G02' ); PRINT( 'G%1m X%m Y%m Z%m I%n J%n K%n F%m', 2, _X2, _Y2, _Z2, _I, _J, _K, _F );
CODE( 'G03' ); PRINT( 'G%1m X%m Y%m Z%m I%n J%n K%n F%m', 3, _X2, _Y2, _Z2, _I, _J, _K, _F );
CODE( 'G04' ); PRINT( 'G4 P%3.1n', _P );

CODE( 'M01' ); PRINT( 'M1' );
CODE( 'M03' ); PRINT( 'M%1m', 3 );
CODE( 'M04' ); PRINT( 'M%1m', 4 );
CODE( 'M05' ); PRINT( 'M5' );
CODE( 'M08' ); PRINT( 'M8' );
CODE( 'M09' ); PRINT( 'M9' );
CODE( 'M30' ); PRINT( 'M30' );

CODE( 'Offset' ); PRINT( 'G54 X%m Y%m Z%m', _dX, _dY, _dZ );
CODE( 'Rotate' ); PRINT( 'G73 H%f', _A );

CODE( 'CallSubProg' ); PRINT( 'M98 P%4f', 1000+_P );
CODE( 'BegSubProg' ); FORMAT( number=0 );
                      PRINT( '( ------------------ )' );
                      PRINT( '( SubProgram %3f )', _P );
                      PRINT( '( ------------------ )' );
                      FORMAT( number=1 );
                      PRINT( 'O%4f', 1000+_P );
CODE( 'EndSubProg' ); PRINT( 'M99' );

CODE( 'End' ); FORMAT( number=0 );
               PRINT( '%' );
               FORMAT( number=1 );


CYCLE( '73', 'Цикл высокоскоростного сверления с периодическим выводом сверла', 'Drilling' );
  COMMENT( 'В этом цикле выполняется высокоскоростное сверление с периодическим выводом сверла' );
  FEATURE( 'HOLE' );
  TRAJECTORY( 'UNIVERSAL_DRILLING', _FWRK=F, _DEPZ=Q, _CHIP=1, _GAPZ=1 );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( Q, O, 'Глубина прохода для каждой подачи при резании', , _DEPZ );
    PARAM( F, s, 'Рабочая подача', _FEED, _FEED );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G73 X%m Y%m Z%f R%f Q%f F%f K%f', X, Y, Z, R, Q, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '74', 'Цикл нарезания левой резьбы', 'Threading_Tap_Die' );
  COMMENT( 'В этом цикле выполняется нарезание левой резьбы' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( P, s, 'Время задержки' );
    PARAM( F, s, 'Рабочая подача', _FEED );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G74 X%m Y%m Z%f R%f P%f F%f K%f', X, Y, Z, R, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '76', 'Цикл чистового растачивания', 'Drilling' );
  COMMENT( 'В цикле чистового растачивания отверстие растачивается с высокой точностью' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( Q, s, 'Величина сдвига у основания отверстия' );
    PARAM( P, s, 'Время задержки у основания отверстия' );
    PARAM( F, s, 'Рабочая подача' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G76 X%m Y%m Z%f R%f Q%f P%f F%f K%f', X, Y, Z, R, Q, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '81', 'Цикл сверления, выборочное сверление', 'Drilling' );
  COMMENT( 'Этот цикл используется для обычного сверления' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY' );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( F, s, 'Рабочая подача' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G81 X%m Y%m Z%f R%f F%f K%f', X, Y, Z, R, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '82', 'Цикл сверления, Цикл обратного растачивания', 'Drilling' );
  COMMENT( 'Этот цикл используется для более точного сверления по отношению к глубине' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( P, s, 'Время задержки у основания отверстия' );
    PARAM( F, s, 'Рабочая подача' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G82 X%m Y%m Z%f R%f P%f F%f K%f', X, Y, Z, R, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '83', 'Цикл сверления с периодическим выводом сверла', 'Drilling' );
  COMMENT( 'Этот цикл выполняет сверление с периодическим выводом сверла' );
  FEATURE( 'HOLE' );
  TRAJECTORY( 'UNIVERSAL_DRILLING', _FWRK=F, _DEPZ=Q, _CHIP=1, _GAPZ=1 );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( Q, O, 'Глубина прохода для каждой подачи при резании', , _DEPZ );
    PARAM( F, s, 'Рабочая подача', _FEED, _FEED );
    PARAM( I, s, 'Скорость прямого или обратного прохода' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G83 X%m Y%m Z%f R%f Q%f F%f I%f K%f', X, Y, Z, R, Q, F, I, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '84', 'Цикл нарезания резьбы', 'Threading_Tap_Die' );
  COMMENT( 'В этом цикле выполняется нарезание резьбы метчиком' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( P, s, 'Время задержки' );
    PARAM( F, A, 'Рабочая подача' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G84 X%m Y%m Z%f R%f P%f F%f K%f', X, Y, Z, R, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '85', 'Цикл растачивания', 'Drilling' );
  COMMENT( 'Этот цикл используется для растачивания отверстия' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G85 X%m Y%m Z%f R%f F%f K%f', X, Y, Z, R, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '86', 'Цикл растачивания', 'Drilling' );
  COMMENT( 'Этот цикл используется для растачивания отверстия' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G86 X%m Y%m Z%f R%f F%f K%f', X, Y, Z, R, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '87', 'Цикл обратного растачивания', 'Drilling' );
  COMMENT( 'Этот цикл выполняет точное растачивание' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( Q, s, 'Величина сдвига инструмента' );
    PARAM( P, s, 'Время задержки' );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G87 X%m Y%m Z%f R%f Q%f P%f F%f K%f', X, Y, Z, R, Q, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '88', 'Цикл растачивания', 'Drilling' );
  COMMENT( 'Этот цикл используется для растачивания отверстия' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( P, s, 'Время задержки у основания отверстия' );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G88 X%m Y%m Z%f R%f P%f F%f K%f', X, Y, Z, R, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE( '89', 'Цикл растачивания', 'Drilling' );
  COMMENT( 'Этот цикл используется для растачивания отверстия' );
  FEATURE( 'HOLE' );
  TRAJECTORY( '' );
  TOOL( 'Drill' );
    PARAM( X, A, 'Позиция отверстия', , _FX );
    PARAM( Y, A, 'Позиция отверстия', , _FY );
    PARAM( Z, A, 'Расстояние от точки R до основания отверстия', , -_FH-_VREZ );
    PARAM( R, A, 'Расстояние от исходного уровня до уровня точки R', , -_ZSAF+_FZ+_VREZ );
    PARAM( P, s, 'Время задержки у основания отверстия' );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( K, s, 'Число повторов' );
  PRINT( 'G89 X%m Y%m Z%f R%f P%f F%f K%f', X, Y, Z, R, P, F, K );
  PRINT_PNT( 'X%m Y%m', X, Y );
  PRINT_END( 'G80' );


CYCLE_GROUP( 'RECT_FACE', 'Прямоугольный торец (цапфа)', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования прямоугольной поверхности. Позволяет комбинировать различные типы обработки поверхности (черновую и чистовую) применительно к фигуре квадрат.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1020, s, 'Торцевая обработка - черновая', 1 );
    PARAM( 1021, s, 'Торцевая обработка - чистовая' );
    PARAM( 1220, s, 'Квадрат - фиксированная фигура', 1 );


CYCLE_GROUP( 'CIRC_FACE', 'Круглый торец (цапфа)', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования круглой поверхности. Позволяет комбинировать различные типы обработки поверхности (черновую и чистовую) применительно к фигуре круг.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1020, s, 'Торцевая обработка - черновая', 1 );
    PARAM( 1021, s, 'Торцевая обработка - чистовая' );
    PARAM( 1221, s, 'Круг - фиксированная фигура', 1 );


CYCLE_GROUP( 'GROUP_POCKET', 'Группа карманов', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования карманов в форме прямоугольника, круга и канавки в рамках одной обработки. Позволяет комбинировать различные типы обработки карманов применительно к указанным фигурам.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1044, s, 'Фрезерование глубоких выемок - центровка' );
    PARAM( 1045, s, 'Фрезерование глубоких выемок - сверление' );
    PARAM( 1040, s, 'Фрезерование глубоких выемок - черновое', 1 );
    PARAM( 1042, s, 'Фрезерование глубоких выемок - чистовая обработка сбоку' );
    PARAM( 1041, s, 'Фрезерование глубоких выемок - чистовая обработка низа' );
    PARAM( 1043, s, 'Фрезерование глубоких выемок - снятие фаски' );
    PARAM( 1220, s, 'Квадрат - фиксированная фигура', 1 );
    PARAM( 1221, s, 'Круг - фиксированная фигура', 1 );
    PARAM( 1222, s, 'Канавка - фиксированная фигура', 1 );


CYCLE_GROUP( 'RECT_POCKET', 'Прямоугольный карман', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования прямоугольного кармана. Позволяет комбинировать различные типы обработки кармана применительно к фигуре квадрат.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1044, s, 'Фрезерование глубоких выемок - центровка' );
    PARAM( 1045, s, 'Фрезерование глубоких выемок - сверление' );
    PARAM( 1040, s, 'Фрезерование глубоких выемок - черновое', 1 );
    PARAM( 1042, s, 'Фрезерование глубоких выемок - чистовая обработка сбоку' );
    PARAM( 1041, s, 'Фрезерование глубоких выемок - чистовая обработка низа' );
    PARAM( 1043, s, 'Фрезерование глубоких выемок - снятие фаски' );
    PARAM( 1220, s, 'Квадрат - фиксированная фигура', 1 );


CYCLE_GROUP( 'CIRC_POCKET', 'Круглый карман', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования круглого кармана. Позволяет комбинировать различные типы обработки кармана применительно к фигуре круг.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1044, s, 'Фрезерование глубоких выемок - центровка' );
    PARAM( 1045, s, 'Фрезерование глубоких выемок - сверление' );
    PARAM( 1040, s, 'Фрезерование глубоких выемок - черновое', 1 );
    PARAM( 1042, s, 'Фрезерование глубоких выемок - чистовая обработка сбоку' );
    PARAM( 1041, s, 'Фрезерование глубоких выемок - чистовая обработка низа' );
    PARAM( 1043, s, 'Фрезерование глубоких выемок - снятие фаски' );
    PARAM( 1221, s, 'Круг - фиксированная фигура', 1 );


CYCLE_GROUP( 'SLOT_POCKET', 'Канавка', 'Milling' );
  COMMENT( 'Групповой цикл для фрезерования канавки. Позволяет комбинировать различные типы обработки применительно к фигуре канавка.' );
  FEATURE( '' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( 1044, s, 'Фрезерование глубоких выемок - центровка' );
    PARAM( 1045, s, 'Фрезерование глубоких выемок - сверление' );
    PARAM( 1040, s, 'Фрезерование глубоких выемок - черновое', 1 );
    PARAM( 1042, s, 'Фрезерование глубоких выемок - чистовая обработка сбоку' );
    PARAM( 1041, s, 'Фрезерование глубоких выемок - чистовая обработка низа' );
    PARAM( 1043, s, 'Фрезерование глубоких выемок - снятие фаски' );
    PARAM( 1222, s, 'Канавка - фиксированная фигура', 1 );


CYCLE_PREPARE( '1020', 'Торцевая обработка - черновая', 'Milling' );
  COMMENT( 'Подготовительный цикл для Торцевой обработки. Цикл задает параметры черновой обработки' );
  FEATURE( 'FACE_RECT | FACE_CIRC' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( T, A, 'Величина съема при черновой обработке', , _FH );
    PARAM( J, s, 'Глубина резания вдоль оси инструмента' );
    PARAM( H, s, 'Величина чистовой обработки' );
    PARAM( F, s, 'Скорость подачи при резании вдоль радиуса инструмента' );
    PARAM( D, A, 'Диаметр инструмента', , _DIAM );
    PARAM( L, O, 'Величина резания в направлении радиуса инструмента' );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'Резание в плоскости X-Y' );
        value( '1 : Однонаправленное', 1 );
        value( '2 : Двунаправленное', 2 );
    PARAM( M, O, 'Расстояние от заготовки до точки врезания в плоскости XY' );
    PARAM( B, s, 'Start Point' );
        value( '1 : Запуск резания в точке 1', 1 );
        value( '2 : Запуск резания в точке 2', 2 );
        value( '3 : Запуск резания в точке 3', 3 );
        value( '4 : Запуск резания в точке 4', 4 );
  PRINT( 'G1020 T%f J%f H%f F%f D%f L%f C%f W%f M%f B%f', T, J, H, F, D, L, C, W, M, B );
  FORMAT( print= _FT==_FACE_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_FACE_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print=1 );


CYCLE_PREPARE( '1021', 'Торцевая обработка - чистовая', 'Milling' );
  COMMENT( 'Подготовительный цикл для Торцевой обработки. Цикл задает параметры чистовой обработки' );
  FEATURE( 'FACE_RECT | FACE_CIRC' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( F, s, 'Скорость подачи при резании вдоль радиуса инструмента' );
    PARAM( D, A, 'Диаметр инструмента', , _DIAM );
    PARAM( L, O, 'Величина резания в направлении радиуса инструмента' );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'Резание в плоскости X-Y' );
        value( '1 : Однонаправленное', 1 );
        value( '2 : Двунаправленное', 2 );
    PARAM( M, O, 'Расстояние от заготовки до точки врезания в плоскости XY' );
    PARAM( B, s, 'Start Point' );
        value( '1 : Запуск резания в точке 1', 1 );
        value( '2 : Запуск резания в точке 2', 2 );
        value( '3 : Запуск резания в точке 3', 3 );
        value( '4 : Запуск резания в точке 4', 4 );
  PRINT( 'G1021 F%f D%f L%f C%f W%f M%f B%f', F, D, L, C, W, M, B );
  FORMAT( print= _FT==_FACE_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_FACE_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print=1 );


CYCLE_PREPARE( '1040', 'Фрезерование глубоких выемок - черновое', 'Milling' );
  COMMENT( 'Подготовительный цикл для Фрезерования глубоких выемок. Цикл задает параметры черновой обработки' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'POCKET_MILLING', _VOLM=1, _DEPZ=J, _KOFR=2*L/_DIAM,  _CDIR=0, _BEGN=0, _RADP=1, _FWRK=F,
                                _FINZ=0, _USID=K, _UBOT=H, _FFIN=0, _SFIN=_SPIN,
                                _PLNG=1, _DMHL=0, _ANHL=0, _LRCP=0, _FPLN=E );
  TOOL( 'Mill' );
    PARAM( T, A, 'Величина съема выемки, расстояние от основания выемки', , _FH );
    PARAM( J, s, 'Глубина резания вдоль оси инструмента', , _DEPZ );
    PARAM( H, s, 'Величина чистовой обработки части основания', , _UBOT );
    PARAM( K, s, 'Величина чистовой обработки боковой стенки', , _USID );
    PARAM( F, s, 'Скорость подачи вдоль радиуса инструмента', , _FEED );
    PARAM( E, s, 'Скорость подачи вдоль оси инструмента (Z)', , _FPLN );
    PARAM( D, A, 'Диаметр инструмента', , _DIAM );
    PARAM( L, s, 'Величина резания в направлении радиуса инструмента', , _KOFR*_DIAM/2 );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'UP CUT/DOWN CUT' );
        value( '1: Выполните резание вниз', 1 );
        value( '2: Выполните резание вверх', 2 );
  PRINT( 'G1040 T%f J%f H%f K%f F%f E%f D%f L%f C%f W%f', T, J, H, K, F, E, D, L, C, W );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE_PREPARE( '1041', 'Фрезерование глубоких выемок - чистовая низа', 'Milling' );
  COMMENT( 'Подготовительный цикл для Фрезерования глубоких выемок. Цикл задает параметры чистовой обработки дна выемки' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'POCKET_MILLING', _VOLM=4, _DEPZ=0, _KOFR=2*L/_DIAM,  _CDIR=0, _BEGN=0, _RADP=1, _FWRK=_FEED,
                                _FINZ=0, _USID=K, _UBOT=T, _FFIN=F, _SFIN=_SPIN,
                                _PLNG=1, _DMHL=0, _ANHL=0, _LRCP=0, _FPLN=E );
  TOOL( 'Mill' );
    PARAM( T, s, 'Величина съема у основания', , _UBOT );
    PARAM( K, s, 'Величина чистовой обработки боковой стенки', , _USID );
    PARAM( F, s, 'Скорость подачи вдоль радиуса инструмента', , _FFIN );
    PARAM( E, s, 'Скорость подачи вдоль оси инструмента (Z)', , _FPLN );
    PARAM( D, A, 'Диаметр инструмента', , _DIAM );
    PARAM( L, s, 'Величина резания в направлении радиуса инструмента', , _KOFR*_DIAM/2 );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'UP CUT/DOWN CUT' );
        value( '1: Выполните резание вниз', 1 );
        value( '2: Выполните резание вверх', 2 );
  PRINT( 'G1041 T%f K%f F%f E%f D%f L%f C%f W%f', T, K, F, E, D, L, C, W );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE_PREPARE( '1042', 'Фрезерование глубоких выемок - чистовая сбоку', 'Milling' );
  COMMENT( 'Подготовительный цикл для Фрезерования глубоких выемок. Цикл задает параметры чистовой обработки боковых сторон выемки' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'POCKET_MILLING', _VOLM=3, _DEPZ=0, _KOFR=0, _CDIR=0, _BEGN=0, _RADP=R, _FWRK=_FEED,
                                _FINZ=0, _USID=0, _UBOT=T, _FFIN=F, _SFIN=_SPIN,
                                _PLNG=1, _DMHL=0, _ANHL=0, _LRCP=0, _FPLN=E );
  TOOL( 'Mill' );
    PARAM( T, s, 'Величина съема у основания', , _UBOT );
    PARAM( F, s, 'Скорость подачи вдоль радиуса инструмента', , _FFIN );
    PARAM( E, s, 'Скорость подачи вдоль оси инструмента (Z)', , _FPLN );
    PARAM( D, A, 'Диаметр инструмента', , _DIAM );
    PARAM( S, s, 'Номер коррекции на режущий инструмент-R' );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'UP CUT/DOWN CUT' );
        value( '1: Выполните резание вниз', 1 );
        value( '2: Выполните резание вверх', 2 );
    PARAM( R, s, 'Радиус дуги перемещения приближения и отвода', , _RADP );
  PRINT( 'G1042 T%f F%f E%f D%f S%f C%f W%f R%f', T, F, E, D, S, C, W, R );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE_PREPARE( '1043', 'Фрезерование глубоких выемок - снятие фаски', 'Milling' );
  COMMENT( 'Подготовительный цикл для обработки глубоких выемок. Цикл задает параметры фрезерования фаски' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'CONTOUR_MILLING' );
  TOOL( 'Mill' );
    PARAM( F, s, 'Скорость подачи вдоль радиуса инструмента' );
    PARAM( E, s, 'Скорость подачи вдоль оси инструмента' );
    PARAM( Z, s, 'Величина снятия фаски вверху боковой стенки' );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( W, s, 'UP CUT/DOWN CUT' );
        value( '1: Выполните резание вниз', 1 );
        value( '2: Выполните резание вверх', 2 );
    PARAM( M, s, 'Радиус дуги перемещения приближения' );
    PARAM( K, s, 'Диаметр конца инструмента для снятия фаски' );
    PARAM( H, s, 'Величина зазора у конца инструмента для снятия фасок' );
  PRINT( 'G1043 F%f E%f Z%f C%f W%f M%f K%f H%f', F, E, Z, C, W, M, K, H );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE_PREPARE( '1044', 'Фрезерование глубоких выемок - центровка', 'Milling' );
  COMMENT( 'Подготовительный цикл для обработки глубоких выемок. Цикл задает параметры центровки отверстия сверлением. Цикл не содержит параметров' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'UNIVERSAL_DRILLING' );
  TOOL( 'Mill' );
  PRINT( 'G1044' );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE_PREPARE( '1045', 'Фрезерование глубоких выемок - сверление', 'Milling' );
  COMMENT( 'Подготовительный цикл для обработки глубоких выемок. Цикл задает параметры сверления отверстия' );
  FEATURE( 'POCKET_RECT | POCKET_CIRC | POCKET_SLOT' );
  TRAJECTORY( 'UNIVERSAL_DRILLING', _FEED=F, _DEPZ=Q, _CHIP=R, _GAPZ=1 );
  TOOL( 'Mill' );
    PARAM( T, s, 'Величина съема выемки' );
    PARAM( C, A, 'Расстояние от заготовки до точки приближения вдоль оси Z', , _VREZ );
    PARAM( R, s, 'MACHINING TYPE', , _CHIP );
        value( '1: Нормальное сверление без задержки', 1, 0 );
        value( '3: Сверление с периодическим выводом сверла без задержки', 3, 1 );
    PARAM( F, s, 'Скорость подачи при резании' );
    PARAM( Q, s, 'Резание на глубину одного движения резания' );
  PRINT( 'G1045 T%f C%f R%f F%f Q%f', T, C, R, F, Q );
  FORMAT( print= _FT==_POCKET_RECT );
  PRINT( 'G1220 H%f V%f U%f W%f B%f L%f A%f R%f', _FX, _FY, _FL, _FW, _FZ, -_FH, _FA, _FR );
  FORMAT( print= _FT==_POCKET_CIRC );
  PRINT( 'G1221 H%f V%f R%f B%f L%f', _FX, _FY, _FR, _FZ, -_FH );
  FORMAT( print= _FT==_POCKET_SLOT );
  PRINT( 'G1222 H%f V%f U%f R%f B%f L%f A%f', _FX, _FY, _FL-_FW, _FR, _FZ, -_FH, _FA );
  FORMAT( print=1 );


CYCLE( '1220', 'Квадрат - фиксированная фигура', 'Milling' );
  COMMENT( 'Цикл задает параметры прямоугольного кармана' );
  FEATURE( 'POCKET_RECT | FACE_RECT' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( H, A, 'Координата X точки центра квадрата' );
    PARAM( V, A, 'Координата Y точки центра квадрата' );
    PARAM( U, A, 'Длина стороны квадрата вдоль оси X' );
    PARAM( W, A, 'Длина стороны квадрата вдоль оси Y' );
    PARAM( В, A, 'Координата Z поверхности заготовки' );
    PARAM( L, A, 'Глубина выемки' );
    PARAM( A, A, 'Угол по по тношению к оси X' );
    PARAM( R, A, 'Радиус каждого угла квадрата' );
  PRINT( '' );

CYCLE( '1221', 'Круг - фиксированная фигура', 'Milling' );
  COMMENT( 'Цикл задает параметры круглого кармана' );
  FEATURE( 'POCKET_CIRC | FACE_CIRC' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( H, A, 'Координата X точки центра окружности' );
    PARAM( V, A, 'Координата Y точки центра окружности' );
    PARAM( R, A, 'Радиус круга' );
    PARAM( В, A, 'Координата Z поверхности заготовки' );
    PARAM( L, A, 'Глубина выемки' );
  PRINT( '' );

CYCLE( '1222', 'Канавка - фиксированная фигура', 'Milling' );
  COMMENT( 'Цикл задает параметры кармана в форме прямой канавки' );
  FEATURE( 'POCKET_SLOT' );
  TRAJECTORY( '' );
  TOOL( 'Mill' );
    PARAM( H, A, 'Координата X точки центра 1-й окружности' );
    PARAM( V, A, 'Координата Y точки центра 1-й окружности' );
    PARAM( U, A, 'Расстояние между центрами 1-ой и 2-ой окружностей' );
    PARAM( R, A, 'Радиус 2-х окружностей' );
    PARAM( В, A, 'Координата Z поверхности заготовки' );
    PARAM( L, A, 'Глубина выемки' );
    PARAM( A, A, 'Угол по отношению к оси X вокруг центра 1-ой окружности' );
  PRINT( '' );

END_MACHINE_CYCLES


"""
from math import *
import sys,string

# Получение очередного кадра ---------------
def get_parse():
 while 1:
  s = filein.readline()
  if len(s)==0: return ""
  if s.find("BEG_SPIRAL")>0 or s.find("END_SPIRAL")>0:
      continue
  return s

# ------------------------------------------
if len(sys.argv)>2: path=sys.argv[2]
else: path=""
filein=open(path+"input.tmp", 'r')
fileout=open(path+"output.tmp", 'w')

while 1:
    ka=get_parse()
    if len(ka)==0: break
    fileout.write(ka)
fileout.write("\n")
print("OK!")
