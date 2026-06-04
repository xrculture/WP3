// import { Html, useProgress } from "@react-three/drei";

// export default function LoadingIndicator() {
//     const { progress } = useProgress()

//     return (
//         <Html
//             center>{progress}
//             % loaded
//         </Html>
//     );
// }

type Props = {
    percentage: number;
    color: string;
};

function cleanPercentage(percentage: number) {
    const isNegativeOrNaN = !Number.isFinite(+percentage) || percentage < 0; // we can set non-numbers to 0 here
    const isTooHigh = percentage > 100;

    return isNegativeOrNaN ? 0 : isTooHigh ? 100 : +percentage;
}

export default function CircularProgressBar({ percentage, color }: Props) {
    const cleanedPercentage = cleanPercentage(percentage);

    const r = 70;
    const circ = 2 * Math.PI * r;
    const strokePct = ((100 - cleanedPercentage) * circ) / 100; // where stroke will start, e.g. from 15% to 100%.

    return (
        <div
            className="absolute flex size-full justify-center items-center left-1/2 top-1/2 transform -translate-x-1/2 -translate-y-1/2">
            <svg
                width={200} height={200}>
                <g
                    transform={`rotate(-90 ${"100 100"})`}>
                    <circle
                        r={r}
                        cx={100}
                        cy={100}
                        fill="transparent"
                        stroke="lightgrey"
                        strokeWidth="20px"
                        strokeDasharray={circ}
                        strokeDashoffset={0} />

                    <circle
                        r={r}
                        cx={100}
                        cy={100}
                        fill="transparent"
                        stroke={strokePct !== circ ? color : ""} // remove colour as 0% sets full circumference
                        strokeWidth="20px"
                        strokeDasharray={circ}
                        strokeDashoffset={percentage ? strokePct : 0} />
                </g>

                <text
                    x="50%"
                    y="50%"
                    dominantBaseline="central"
                    textAnchor="middle"
                    fontSize="1.5em">
                    {cleanedPercentage.toFixed(0)}%
                </text>
            </svg>
        </div>
    );
}